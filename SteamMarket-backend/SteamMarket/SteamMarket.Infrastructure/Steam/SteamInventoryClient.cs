using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SteamMarket.Application.Common.Interfaces;
using SteamMarket.Application.Inventory;
using SteamMarket.Domain.Entities;
using SteamMarket.Infrastructure.Persistence;
using SteamMarket.Infrastructure.Steam.Models;

namespace SteamMarket.Infrastructure.Steam;

/// <summary>
/// Implementacion real del puerto ISteamInventoryClient.
/// Llama al endpoint publico de Steam, parsea el JSON y lo convierte a entidades del dominio.
///
/// IMPORTANTE (paginacion): Steam ya NO devuelve todo el inventario en un solo pedido con
/// count=5000 (eso ahora responde "null"). Hay que pedir de a paginas chicas (count=500) y,
/// si la respuesta trae more_items=1, repetir el pedido con start_assetid=last_assetid hasta
/// que no queden mas paginas.
///
/// IMPORTANTE (rate limit): el endpoint de inventario de Steam limita agresivo por IP (429).
/// Por eso, igual que con los precios (ver SteamMarketPriceProvider), el inventario se cachea
/// en SQLite:
///   1) Si hay una copia cacheada mas fresca que InventoryCacheOptions.CacheMinutes, se sirve
///      esa y no se le pide nada a Steam. Asi una pagina que se recarga seguido (o un backend
///      que se reinicia) no vuelve a generar una rafaga de pedidos.
///   2) Si Steam falla o responde 429 y HAY una copia cacheada (aunque este vencida), se sirve
///      esa copia vieja en vez de mostrarle un error al usuario.
///   3) Solo si Steam falla y nunca hubo una copia cacheada, se devuelve el error.
///
/// IMPORTANTE (multiples usuarios): la cache de arriba es por usuario, asi que NO alcanza
/// si muchos usuarios DISTINTOS piden su inventario por primera vez casi al mismo tiempo:
/// Steam limita por IP de nuestro servidor, no por cuenta de Steam. Por eso ademas hay un
/// throttle global (mismo patron que SteamMarketPriceProvider): un SemaphoreSlim estatico
/// asegura que solo se este trayendo UN inventario a la vez de Steam para todo el proceso,
/// con un espacio minimo entre cada fetch completo, sin importar cuantos usuarios pidan
/// al mismo tiempo.
/// </summary>
public sealed class SteamInventoryClient : ISteamInventoryClient
{
    // Dota 2 = appid 570. El contexto 2 es donde viven los items de Dota.
    private const int DotaAppId = 570;
    private const int ContextId = 2;
    private const int PageSize = 500;
    private const int MaxPages = 50; // tope de seguridad: 50 * 500 = 25000 items, de sobra.
    private static readonly TimeSpan DelayBetweenPages = TimeSpan.FromMilliseconds(800);

    // Throttle GLOBAL (todo el proceso, todos los usuarios): estatico a proposito, igual que
    // en SteamMarketPriceProvider. Evita que N usuarios pidiendo inventario al mismo tiempo
    // generen una rafaga de pedidos a Steam desde nuestra IP.
    private static readonly SemaphoreSlim FetchGate = new(1, 1);
    private static readonly TimeSpan MinDelayBetweenFetches = TimeSpan.FromSeconds(2);
    private static DateTime _lastFetchStartUtc = DateTime.MinValue;

    private readonly HttpClient _http;
    private readonly ILogger<SteamInventoryClient> _logger;
    private readonly SteamMarketDbContext _db;
    private readonly InventoryCacheOptions _options;

    public SteamInventoryClient(
        HttpClient http,
        ILogger<SteamInventoryClient> logger,
        SteamMarketDbContext db,
        InventoryCacheOptions options)
    {
        _http = http;
        _logger = logger;
        _db = db;
        _options = options;
    }

    public async Task<SteamFetchResult> GetDotaInventoryAsync(string steamId64, bool force = false, CancellationToken ct = default)
    {
        var cached = await _db.Inventories.FindAsync([steamId64], ct);
        var cutoff = DateTime.UtcNow.AddMinutes(-Math.Max(1, _options.CacheMinutes));

        if (!force && cached is not null && cached.FetchedAtUtc >= cutoff)
        {
            _logger.LogInformation(
                "Inventario de {SteamId} servido desde cache SQLite (fresco, {Age} min).",
                steamId64, (DateTime.UtcNow - cached.FetchedAtUtc).TotalMinutes);
            return SteamFetchResult.Ok(Deserialize(cached.ItemsJson), cached.FetchedAtUtc);
        }

        var fetched = await FetchFromSteamAsync(steamId64, ct);

        if (!fetched.Success)
        {
            // Steam fallo (429, privado, etc.). Si hay algo cacheado, aunque este vencido,
            // preferimos mostrar eso a mostrarle un error al usuario.
            if (cached is not null)
            {
                _logger.LogWarning(
                    "Steam fallo para {SteamId} ({Error}); sirviendo inventario cacheado vencido de {FetchedAt} UTC.",
                    steamId64, fetched.Error, cached.FetchedAtUtc);
                return SteamFetchResult.Ok(Deserialize(cached.ItemsJson), cached.FetchedAtUtc);
            }

            return fetched;
        }

        var fetchedAtUtc = DateTime.UtcNow;
        await UpsertCacheAsync(steamId64, cached, fetched.Items, fetchedAtUtc, ct);
        return SteamFetchResult.Ok(fetched.Items, fetchedAtUtc);
    }

    private async Task<SteamFetchResult> FetchFromSteamAsync(string steamId64, CancellationToken ct)
    {
        // Un solo fetch de inventario a la vez en TODO el proceso (no solo por usuario).
        await FetchGate.WaitAsync(ct);
        try
        {
            var elapsedSinceLast = DateTime.UtcNow - _lastFetchStartUtc;
            if (elapsedSinceLast < MinDelayBetweenFetches)
                await Task.Delay(MinDelayBetweenFetches - elapsedSinceLast, ct);

            _lastFetchStartUtc = DateTime.UtcNow;

            return await FetchPagesAsync(steamId64, ct);
        }
        finally
        {
            FetchGate.Release();
        }
    }

    private async Task<SteamFetchResult> FetchPagesAsync(string steamId64, CancellationToken ct)
    {
        var allAssets = new List<SteamAsset>();
        var allDescriptions = new List<SteamDescription>();

        string? startAssetId = null;

        for (var page = 0; page < MaxPages; page++)
        {
            if (page > 0)
                await Task.Delay(DelayBetweenPages, ct);

            var url = $"https://steamcommunity.com/inventory/{steamId64}/{DotaAppId}/{ContextId}" +
                      $"?l=english&count={PageSize}" +
                      (startAssetId is null ? "" : $"&start_assetid={startAssetId}");

            HttpResponseMessage resp;
            try
            {
                resp = await _http.GetAsync(url, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo al contactar a Steam (pagina {Page})", page);
                return SteamFetchResult.Fail($"No se pudo contactar a Steam: {ex.Message}");
            }

            if (resp.StatusCode == HttpStatusCode.Forbidden)
                return SteamFetchResult.Fail(
                    "El inventario es privado o Steam limito las peticiones. " +
                    "Ponlo publico (Perfil > Editar > Privacidad > Inventario) e intenta de nuevo.");

            if (resp.StatusCode == HttpStatusCode.TooManyRequests)
                return SteamFetchResult.Fail("Steam limito las peticiones (429). Espera un momento.");

            if (!resp.IsSuccessStatusCode)
            {
                var errorBody = await resp.Content.ReadAsStringAsync(ct);
                var snippet = errorBody.Length > 300 ? errorBody[..300] : errorBody;
                _logger.LogWarning(
                    "Steam respondio {StatusCode} para {Url} (pagina {Page}). Cuerpo: {Body}",
                    (int)resp.StatusCode, url, page, snippet);

                return SteamFetchResult.Fail(
                    $"Steam respondio con codigo {(int)resp.StatusCode} (pagina {page}). Detalle: {snippet}");
            }

            var json = await resp.Content.ReadAsStringAsync(ct);

            SteamInventoryResponse? data;
            try
            {
                data = JsonSerializer.Deserialize<SteamInventoryResponse>(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo al parsear la respuesta de Steam (pagina {Page})", page);
                return SteamFetchResult.Fail($"Error al leer la respuesta de Steam: {ex.Message}");
            }

            // Primera pagina vacia = inventario vacio de verdad, no es error.
            if (data?.Assets is null || data.Assets.Count == 0)
            {
                if (page == 0)
                    return SteamFetchResult.Ok(Array.Empty<InventoryItem>());
                break;
            }

            allAssets.AddRange(data.Assets);
            if (data.Descriptions is not null)
                allDescriptions.AddRange(data.Descriptions);

            _logger.LogInformation(
                "Pagina {Page}: {AssetCount} assets acumulados, more_items={More}",
                page, allAssets.Count, data.MoreItems);

            if (data.MoreItems != 1 || string.IsNullOrEmpty(data.LastAssetId))
                break; // no hay mas paginas

            startAssetId = data.LastAssetId;
        }

        var descriptions = allDescriptions
            .GroupBy(d => $"{d.ClassId}_{d.InstanceId}")
            .ToDictionary(g => g.Key, g => g.First());

        var items = new List<InventoryItem>();
        foreach (var asset in allAssets)
        {
            var key = $"{asset.ClassId}_{asset.InstanceId}";
            if (!descriptions.TryGetValue(key, out var desc))
                continue;

            // "Rarity" (Common, Rare, Mythical, Legendary, Immortal, ...) y "Quality"
            // (Arcana, Genuine, Unusual, ...) vienen como tags separados en el JSON de Steam.
            var rarityTag = desc.Tags?.FirstOrDefault(t => t.Category == "Rarity");
            var qualityTag = desc.Tags?.FirstOrDefault(t => t.Category == "Quality");
            var heroTag = desc.Tags?.FirstOrDefault(t => t.Category == "Hero");

            items.Add(new InventoryItem
            {
                AssetId = asset.AssetId,
                ClassId = asset.ClassId,
                Name = desc.MarketName ?? desc.Name ?? "Desconocido",
                MarketHashName = desc.MarketHashName ?? string.Empty,
                Type = desc.Type ?? string.Empty,
                Tradable = desc.Tradable == 1,
                Marketable = desc.Marketable == 1,
                IconUrl = string.IsNullOrEmpty(desc.IconUrl)
                    ? null
                    : $"https://community.cloudflare.steamstatic.com/economy/image/{desc.IconUrl}",
                Rarity = rarityTag?.LocalizedTagName,
                Quality = qualityTag?.LocalizedTagName,
                RarityColor = rarityTag?.Color,
                Hero = heroTag?.LocalizedTagName
            });
        }

        // DEBUG temporal: mostramos los tags crudos de un item para confirmar que "Hero" es
        // el nombre de categoria correcto en el JSON de Steam (si el filtro de heroe no
        // funciona, esto revela como se llama realmente esa categoria). Se puede borrar
        // despues de confirmar.
        var sample = allDescriptions.FirstOrDefault(d => d.Tags is { Count: > 0 });
        if (sample?.Tags is not null)
        {
            _logger.LogInformation(
                "DEBUG tags de muestra ({Name}): {Tags}",
                sample.MarketName ?? sample.Name,
                string.Join(" | ", sample.Tags.Select(t => $"{t.Category}={t.LocalizedTagName}")));
        }

        return SteamFetchResult.Ok(items);
    }

    private async Task UpsertCacheAsync(
        string steamId64, CachedInventory? existing, IReadOnlyList<InventoryItem> items, DateTime fetchedAtUtc, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(items);

        if (existing is null)
        {
            _db.Inventories.Add(new CachedInventory
            {
                SteamId64 = steamId64,
                ItemsJson = json,
                FetchedAtUtc = fetchedAtUtc
            });
        }
        else
        {
            existing.ItemsJson = json;
            existing.FetchedAtUtc = fetchedAtUtc;
        }

        await _db.SaveChangesAsync(ct);
    }

    private static IReadOnlyList<InventoryItem> Deserialize(string json) =>
        JsonSerializer.Deserialize<List<InventoryItem>>(json) ?? [];
}
