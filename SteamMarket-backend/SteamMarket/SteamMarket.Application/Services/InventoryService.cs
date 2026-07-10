using Microsoft.Extensions.Logging;
using SteamMarket.Application.Common.Interfaces;
using SteamMarket.Application.DTOs;
using SteamMarket.Application.Pricing;
using SteamMarket.Application.Services.Interfaces;

namespace SteamMarket.Application.Services;

/// <summary>
/// Caso de uso: obtener el inventario de Dota de un usuario y cotizarlo.
/// 1) Trae los items desde Steam (ISteamInventoryClient).
/// 2) Pide precios de referencia en batch (IMarketPriceProvider), solo para items marketable
///    y "valiosos" (InventoryItem.IsHighValue: Immortal/Mythical/Arcana) - son los unicos que
///    el frontend muestra, no tiene sentido gastar cupo de pedidos en comunes/couriers.
/// 3) Aplica la regla de negocio de payout en cada InventoryItem (ApplyPricing, en el dominio).
///
/// IMPORTANTE: esta llamada (la interactiva, la que dispara el usuario al cargar la pagina)
/// pide precios con un tope CHICO (InteractiveMaxLiveFetches) para que la pagina cargue rapido
/// siempre. El resto de los items sin cotizar todavia se van completando solos via
/// InventoryPriceWarmupService (Infrastructure), que corre en segundo plano sin ese apuro.
/// </summary>
public sealed class InventoryService : IInventoryService
{
    private const int InteractiveMaxLiveFetches = 8;

    private readonly ISteamInventoryClient _inventoryClient;
    private readonly IMarketPriceProvider _priceProvider;
    private readonly PricingOptions _pricing;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        ISteamInventoryClient inventoryClient,
        IMarketPriceProvider priceProvider,
        PricingOptions pricing,
        ILogger<InventoryService> logger)
    {
        _inventoryClient = inventoryClient;
        _priceProvider = priceProvider;
        _pricing = pricing;
        _logger = logger;
    }

    public async Task<InventoryResponse> GetDotaInventoryAsync(string steamId64, bool force = false, CancellationToken ct = default)
    {
        var result = await _inventoryClient.GetDotaInventoryAsync(steamId64, force, ct);

        if (!result.Success)
            return InventoryResponse.Fail(result.Error ?? "No se pudo obtener el inventario.");

        var items = result.Items.ToList();

        // Solo tiene sentido cotizar items marketable Y valiosos (lo unico que se muestra),
        // y solo una vez por nombre (deduplicado).
        var namesToPrice = items
            .Where(i => i.Marketable && i.IsHighValue() && !string.IsNullOrWhiteSpace(i.MarketHashName))
            .Select(i => i.MarketHashName)
            .Distinct()
            .ToList();

        var prices = namesToPrice.Count > 0
            ? await _priceProvider.GetPricesAsync(namesToPrice, force, InteractiveMaxLiveFetches, ct)
            : new Dictionary<string, MarketPriceResult>();

        foreach (var item in items)
        {
            prices.TryGetValue(item.MarketHashName, out var quote);

            if (quote is { Success: true, Price: not null })
            {
                item.ApplyPricing(quote.Price.Value, _pricing.Margin);
            }
            // Si no hay cotizacion (no marketable, sin listings, tope de pedidos alcanzado, o
            // Steam limito las peticiones), el item se devuelve sin MarketPrice/PayoutPrice
            // (null) y el frontend lo marca como "sin cotizar".

            // DEBUG temporal: para los items Immortal/Mythical/Arcana, logueamos exactamente
            // por que tienen o no tienen precio, para no tener que adivinar. Se puede borrar
            // despues de confirmar que todo cotiza bien.
            if (item.IsHighValue())
            {
                _logger.LogInformation(
                    "DEBUG precio '{Name}': Marketable={Marketable}, Success={Success}, Price={Price}, FromCache={FromCache}, Error={Error}",
                    item.MarketHashName, item.Marketable, quote?.Success, quote?.Price, quote?.FromCache, quote?.Error);
            }
        }

        var dtos = items.Select(InventoryItemDto.FromDomain).ToList();
        return InventoryResponse.Ok(dtos, result.FetchedAtUtc);
    }
}
