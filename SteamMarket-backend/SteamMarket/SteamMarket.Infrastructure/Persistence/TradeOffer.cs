namespace SteamMarket.Infrastructure.Persistence;

/// <summary>
/// Vincula una SellOrder con la oferta de intercambio real que el bot mando en Steam por ella.
/// El polling (TradeOfferPollingService) revisa periodicamente el Status de las que siguen
/// activas; cuando llega a Accepted, se completa la SellOrder y se acredita la billetera solo.
/// </summary>
public sealed class TradeOffer
{
    public Guid Id { get; set; }
    public Guid SellOrderId { get; set; }
    public ulong TradeOfferId { get; set; }
    public string SteamId64 { get; set; } = string.Empty;

    /// <summary>Numero de BotTradeOfferState (Application.Common.Interfaces). Se guarda como int
    /// simple aca en Infrastructure para no acoplar la entidad de EF al enum de Application.</summary>
    public int Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastCheckedAtUtc { get; set; }
}
