namespace SteamMarket.Infrastructure.Persistence;

/// <summary>
/// Una oferta de intercambio real que el bot mando en Steam, en cualquiera de las dos
/// direcciones que maneja el sitio:
///   - "SellOrder": el bot le PIDE items al usuario (venta confirmada, ver SellOrderService).
///   - "LootBoxRedeem": el bot le ENTREGA un item ganado al usuario (canje de caja, ver
///     LootBoxService), sin pedirle nada a cambio.
/// El polling (TradeOfferPollingService) revisa periodicamente el Status de las que siguen
/// activas y resuelve la SellOrder o el LootBoxWin correspondiente segun el Kind.
/// </summary>
public sealed class TradeOffer
{
    public Guid Id { get; set; }

    /// <summary>"SellOrder" | "LootBoxRedeem".</summary>
    public string Kind { get; set; } = "SellOrder";

    public Guid? SellOrderId { get; set; }
    public Guid? LootBoxWinId { get; set; }

    public ulong TradeOfferId { get; set; }
    public string SteamId64 { get; set; } = string.Empty;

    /// <summary>Numero de BotTradeOfferState (Application.Common.Interfaces). Se guarda como int
    /// simple aca en Infrastructure para no acoplar la entidad de EF al enum de Application.</summary>
    public int Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastCheckedAtUtc { get; set; }
}
