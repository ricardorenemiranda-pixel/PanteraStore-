using SteamMarket.Application.Common.Interfaces;
using SteamMarket.Application.DTOs;
using SteamMarket.Application.Pricing;
using SteamMarket.Application.Services.Interfaces;

namespace SteamMarket.Application.Services;

/// <summary>
/// Caso de uso: confirmar la venta de items seleccionados en el carrito.
/// 1) Re-cotiza SIEMPRE desde el inventario real (cache del backend), nunca confia en un total
///    mandado por el cliente: evita que alguien manipule el precio final desde el navegador.
/// 2) Valida el monto minimo de venta (PricingOptions.MinSaleAmount).
/// 3) Crea la orden en estado "Pending" y devuelve una Trade URL disponible (del pool de cuentas
///    admin, ver IAdminAccountService) para que el usuario mande el trade.
/// 4) El saldo NO se acredita aca: se acredita cuando el admin confirma que recibio el trade
///    (CompleteOrderAsync), para no pagarle a nadie antes de recibir los items de verdad.
/// </summary>
public sealed class SellOrderService : ISellOrderService
{
    private readonly ISellOrderStore _orders;
    private readonly IWalletStore _wallet;
    private readonly IInventoryService _inventory;
    private readonly PricingOptions _pricing;
    private readonly IAdminAccountService _adminAccounts;
    private readonly IUserProfileStore _profiles;
    private readonly ISteamBotService _bot;
    private readonly ITradeOfferStore _tradeOffers;

    public SellOrderService(
        ISellOrderStore orders,
        IWalletStore wallet,
        IInventoryService inventory,
        PricingOptions pricing,
        IAdminAccountService adminAccounts,
        IUserProfileStore profiles,
        ISteamBotService bot,
        ITradeOfferStore tradeOffers)
    {
        _orders = orders;
        _wallet = wallet;
        _inventory = inventory;
        _pricing = pricing;
        _adminAccounts = adminAccounts;
        _profiles = profiles;
        _bot = bot;
        _tradeOffers = tradeOffers;
    }

    public async Task<CreateSellOrderResult> CreateOrderAsync(string steamId64, IReadOnlyList<string> assetIds, CancellationToken ct = default)
    {
        if (assetIds.Count == 0)
            return CreateSellOrderResult.Fail("No seleccionaste ningun item.");

        var adminTradeUrl = await _adminAccounts.GetAvailableTradeUrlAsync(ct);
        if (string.IsNullOrWhiteSpace(adminTradeUrl))
            return CreateSellOrderResult.Fail("El sitio todavia no tiene configurada ninguna cuenta que reciba items. Contacta al administrador.");

        var inventory = await _inventory.GetDotaInventoryAsync(steamId64, force: false, ct);
        if (!inventory.Success)
            return CreateSellOrderResult.Fail(inventory.Error ?? "No se pudo leer tu inventario.");

        var wanted = assetIds.ToHashSet();
        var matched = inventory.Items
            .Where(i => wanted.Contains(i.AssetId) && i.Marketable && i.PayoutPrice is not null)
            .ToList();

        if (matched.Count == 0)
            return CreateSellOrderResult.Fail("Ninguno de los items seleccionados tiene un precio valido ahora mismo.");

        var total = matched.Sum(i => i.PayoutPrice!.Value);
        if (total < _pricing.MinSaleAmount)
            return CreateSellOrderResult.Fail($"El total debe ser mayor a S/ {_pricing.MinSaleAmount:0.00} para procesar la venta.");

        var items = matched.Select(i => new SellOrderItem
        {
            AssetId = i.AssetId,
            Name = i.Name,
            IconUrl = i.IconUrl,
            PayoutPrice = i.PayoutPrice!.Value
        }).ToList();

        var record = await _orders.CreateAsync(steamId64, items, total, ct);
        var dto = ToDto(record);

        // Intenta que el bot le mande la oferta automatica al usuario (solo tiene que aceptarla
        // en Steam). Si el bot no esta listo o el usuario no guardo su Trade URL en su perfil,
        // se cae al flujo manual de siempre: mostrarle el AdminTradeUrl para que el mande el
        // intercambio a mano. La orden queda "Pending" en cualquiera de los dos casos.
        var sellerProfile = await _profiles.GetAsync(steamId64, ct);
        if (string.IsNullOrWhiteSpace(sellerProfile?.TradeUrl))
        {
            return CreateSellOrderResult.Ok(dto, adminTradeUrl, botOfferSent: false,
                botOfferError: "Guarda tu Trade URL en tu perfil para que te mandemos la oferta automatica.");
        }

        if (!_bot.IsReady)
        {
            return CreateSellOrderResult.Ok(dto, adminTradeUrl, botOfferSent: false,
                botOfferError: "El bot no esta conectado ahora mismo.");
        }

        var sendResult = await _bot.SendTradeOfferAsync(steamId64, sellerProfile.TradeUrl!, assetIds, ct);
        if (!sendResult.Success || sendResult.TradeOfferId is null)
        {
            return CreateSellOrderResult.Ok(dto, adminTradeUrl, botOfferSent: false,
                botOfferError: sendResult.Error ?? "No se pudo enviar la oferta automatica.");
        }

        await _tradeOffers.CreateAsync(record.Id, sendResult.TradeOfferId.Value, steamId64, ct);

        return CreateSellOrderResult.Ok(dto, adminTradeUrl, botOfferSent: true);
    }

    public async Task<IReadOnlyList<SellOrderDto>> GetMyOrdersAsync(string steamId64, CancellationToken ct = default)
    {
        var records = await _orders.GetByUserAsync(steamId64, ct);
        return records.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<SellOrderDto>> GetPendingOrdersAsync(CancellationToken ct = default)
    {
        var records = await _orders.GetPendingAsync(ct);
        return records.Select(ToDto).ToList();
    }

    public async Task<bool> CompleteOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(orderId, ct);
        if (order is null || order.Status != "Pending") return false;

        await _wallet.CreditAsync(
            order.SteamId64,
            order.TotalAmount,
            "Sale",
            order.Id.ToString(),
            $"Venta #{order.Id.ToString()[..8]}",
            ct);

        await _orders.UpdateStatusAsync(orderId, "Completed", null, ct);
        return true;
    }

    public async Task<bool> RejectOrderAsync(Guid orderId, string? note, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(orderId, ct);
        if (order is null || order.Status != "Pending") return false;

        await _orders.UpdateStatusAsync(orderId, "Rejected", note, ct);
        return true;
    }

    private static SellOrderDto ToDto(SellOrderRecord r) => new()
    {
        Id = r.Id,
        SteamId = r.SteamId64,
        Items = r.Items.Select(i => new SellOrderItemDto
        {
            AssetId = i.AssetId,
            Name = i.Name,
            IconUrl = i.IconUrl,
            PayoutPrice = i.PayoutPrice
        }).ToList(),
        TotalAmount = r.TotalAmount,
        Status = r.Status,
        CreatedAtUtc = r.CreatedAtUtc,
        ResolvedAtUtc = r.ResolvedAtUtc,
        AdminNote = r.AdminNote
    };
}
