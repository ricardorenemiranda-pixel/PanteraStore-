using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SteamMarket.Application.Common.Interfaces;
using SteamMarket.Application.Pricing;
using SteamMarket.Infrastructure.Persistence;
using SteamMarket.Infrastructure.Pricing.Models;

namespace SteamMarket.Infrastructure.Pricing;

/// <summary>
/// Implementacion real del puerto IMarketPriceProvider.
/// 1) Mira la cache propia (SQLite) primero.
/// 2) Para lo que falta o esta vencido, pega contra steamcommunity.com/market/priceoverview,
///    UNA peticion a la vez con throttling (el endpoint publico rate-limita agresivo).
/// 3) Si Steam responde 429, deja de pedir mas por esta tanda y usa cache vieja como fallback
///    cuando existe, en vez de fallar todo el inventario.
/// </summary>
public sealed class SteamMarketPriceProvider : IMarketPriceProvider
{
    // Dota 2 = appid 570 (mismo que en SteamInventoryClient).
    private const int DotaAppId = 570;

    // Con 1.2s de pausa entre pedidos, cotizar TODO un inventario grande (70+ items) de una
    // sola vez puede tardar mas de un minuto -> el navegador cancela el pedido antes de que
    // termine y ese progreso se pierde. Por eso topamos cuantos precios NUEVOS se piden a
    // Steam en un solo request; el resto queda "sin cotizar" por ahora y se completa solo,
    // de a poco, en los proximos pedidos (los que ya se cotizaron se sirven de cache al toque).
    private const int MaxLiveFetchesPerRequest = 15;

    private readonly HttpClient _http;
    private readonly SteamMarketDbContext _db;
    private readonly PricingOptions _options;
    private readonly ILogger<SteamMarketPriceProvider> _logger;

    // Estatico a proposito: el throttle debe ser global a todo el proceso,
    // sin importar el ciclo de vida (scoped) de esta clase o del DbContext.
    private static readonly SemaphoreSlim ThrottleGate = new(1, 1);
    private static readonly TimeSpan MinDelayBetweenCalls = TimeSpan.FromMilliseconds(1200);
    private static DateTime _lastRequestUtc = DateTime.MinValue;

    public SteamMarketPriceProvider(
        HttpClient http,
        SteamMarketDbContext db,
        PricingOptions options,
        ILogger<SteamMarketPriceProvider> logger)
    {
        _http = http;
        _db = db;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, MarketPriceResult>> GetPricesAsync(
        IEnumerable<string> marketHashNames,
        bool force = false,
        int? maxLiveFetches = null,
        CancellationToken ct = default)
    {
        var fetchLimit = maxLiveFetches ?? MaxLiveFetchesPerRequest;
        var names = marketHashNames.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
        var results = new Dictionary<string, MarketPriceResult>();
        if (names.Count == 0) return results;

        var cutoff = DateTime.UtcNow.AddHours(-Math.Max(0, _options.CacheHours));

        // Solo cuenta como "cacheado" si es de la MISMA moneda configurada ahora mismo.
        // Si en algun momento se cambia Pricing:CurrencyId (ej. de USD a PEN), las filas
        // viejas quedan con el CurrencyId anterior y se ignoran por completo (ni fresh-hit
        // ni fallback stale) en vez de mostrarse disfrazadas de la moneda nueva.
        var cached = await _db.MarketPrices
            .Where(p => names.Contains(p.MarketHashName) && p.CurrencyId == _options.CurrencyId)
            .ToDictionaryAsync(p => p.MarketHashName, ct);

        var toFetch = new List<string>();
        foreach (var name in names)
        {
            // force=true (boton "Actualizar" del frontend) ignora el cache y vuelve a
            // pedirle el precio a Steam, aunque todavia este "fresco" segun CacheHours.
            if (!force && cached.TryGetValue(name, out var row) && row.FetchedAtUtc >= cutoff)
                results[name] = new MarketPriceResult(true, row.Price, true, null);
            else
                toFetch.Add(name);
        }

        var steamRateLimited = false;
        var liveFetches = 0;

        foreach (var name in toFetch)
        {
            if (steamRateLimited)
            {
                results[name] = FallbackToStaleCacheOrFail(cached, name, "Steam limito las peticiones (429).");
                continue;
            }

            if (liveFetches >= fetchLimit)
            {
                // Tope de este pedido alcanzado; no es un error de Steam, solo nos quedamos
                // sin "cupo" esta vez. El siguiente pedido normal (sin force) retoma desde aca.
                results[name] = FallbackToStaleCacheOrFail(
                    cached, name, "Todavia no se pidio (limite de cotizaciones por pedido). Volve a cargar en un rato.");
                continue;
            }

            liveFetches++;
            var quote = await FetchFromSteamAsync(name, ct);

            if (quote.RateLimited)
            {
                steamRateLimited = true;
                results[name] = FallbackToStaleCacheOrFail(cached, name, "Steam limito las peticiones (429).");
                continue;
            }

            if (quote.Price is { } price)
            {
                await UpsertCacheAsync(name, price, ct);
                results[name] = new MarketPriceResult(true, price, false, null);
            }
            else
            {
                results[name] = FallbackToStaleCacheOrFail(cached, name, quote.Error ?? "Sin cotizacion disponible.");
            }
        }

        return results;
    }

    private static MarketPriceResult FallbackToStaleCacheOrFail(
        Dictionary<string, CachedMarketPrice> cached, string name, string error) =>
        cached.TryGetValue(name, out var stale)
            ? new MarketPriceResult(true, stale.Price, true, error)
            : new MarketPriceResult(false, null, false, error);

    private async Task<SteamQuote> FetchFromSteamAsync(string marketHashName, CancellationToken ct)
    {
        await ThrottleAsync(ct);

        var url = $"/market/priceoverview/?appid={DotaAppId}&currency={_options.CurrencyId}" +
                   $"&market_hash_name={Uri.EscapeDataString(marketHashName)}";

        HttpResponseMessage resp;
        try
        {
            resp = await _http.GetAsync(url, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo al pedir precio de {Name}", marketHashName);
            return new SteamQuote(null, false, ex.Message);
        }

        if (resp.StatusCode == HttpStatusCode.TooManyRequests)
            return new SteamQuote(null, true, "429");

        if (!resp.IsSuccessStatusCode)
            return new SteamQuote(null, false, $"HTTP {(int)resp.StatusCode}");

        string json;
        try
        {
            json = await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            return new SteamQuote(null, false, ex.Message);
        }

        SteamPriceOverviewResponse? data;
        try
        {
            data = JsonSerializer.Deserialize<SteamPriceOverviewResponse>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo al parsear precio de {Name}", marketHashName);
            return new SteamQuote(null, false, ex.Message);
        }

        if (data is null || !data.Success)
            return new SteamQuote(null, false, "Steam no devolvio cotizacion (item sin listings recientes).");

        var price = ParsePrice(data.LowestPrice) ?? ParsePrice(data.MedianPrice);
        return new SteamQuote(price, false, price is null ? "No se pudo leer el precio devuelto por Steam." : null);
    }

    private static async Task ThrottleAsync(CancellationToken ct)
    {
        await ThrottleGate.WaitAsync(ct);
        try
        {
            var elapsed = DateTime.UtcNow - _lastRequestUtc;
            if (elapsed < MinDelayBetweenCalls)
                await Task.Delay(MinDelayBetweenCalls - elapsed, ct);

            _lastRequestUtc = DateTime.UtcNow;
        }
        finally
        {
            ThrottleGate.Release();
        }
    }

    private async Task UpsertCacheAsync(string marketHashName, decimal price, CancellationToken ct)
    {
        var existing = await _db.MarketPrices.FindAsync([marketHashName], ct);
        if (existing is null)
        {
            _db.MarketPrices.Add(new CachedMarketPrice
            {
                MarketHashName = marketHashName,
                Price = price,
                FetchedAtUtc = DateTime.UtcNow,
                CurrencyId = _options.CurrencyId
            });
        }
        else
        {
            existing.Price = price;
            existing.FetchedAtUtc = DateTime.UtcNow;
            existing.CurrencyId = _options.CurrencyId;
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Steam devuelve precios como texto formateado con el simbolo de la moneda (ej. "$0.03",
    /// "S/. 15,50", "1.234,56 €"). El separador decimal cambia segun la moneda/region, asi que
    /// en vez de asumir un formato fijo: nos quedamos solo con digitos/puntos/comas, y tratamos
    /// el que aparezca MAS A LA DERECHA (el ultimo "." o ",") como separador decimal; el otro,
    /// si existe, es separador de miles y se descarta.
    /// </summary>
    private static decimal? ParsePrice(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var cleaned = new string(raw.Where(c => char.IsDigit(c) || c is '.' or ',').ToArray());
        if (cleaned.Length == 0) return null;

        var decimalSeparatorIndex = Math.Max(cleaned.LastIndexOf('.'), cleaned.LastIndexOf(','));

        string normalized;
        if (decimalSeparatorIndex == -1)
        {
            normalized = cleaned;
        }
        else
        {
            var integerPart = new string(cleaned[..decimalSeparatorIndex].Where(char.IsDigit).ToArray());
            var decimalPart = new string(cleaned[(decimalSeparatorIndex + 1)..].Where(char.IsDigit).ToArray());
            normalized = $"{integerPart}.{decimalPart}";
        }

        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private sealed record SteamQuote(decimal? Price, bool RateLimited, string? Error);
}
