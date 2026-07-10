namespace SteamMarket.Application.Common.Interfaces;

/// <summary>Vincula una SellOrder con la oferta de intercambio real que el bot mando por ella.</summary>
public sealed class TradeOfferRecord
{
    public Guid Id { get; set; }
    public Guid SellOrderId { get; set; }
    public ulong TradeOfferId { get; set; }
    public string SteamId64 { get; set; } = string.Empty;

    /// <summary>Ultimo estado conocido (numero de BotTradeOfferState), actualizado por el polling.</summary>
    public int Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastCheckedAtUtc { get; set; }
}

/// <summary>Persistencia de las ofertas de intercambio enviadas por el bot, para poder
/// consultarlas periodicamente y saber cuando ya se pueden dar por completadas.</summary>
public interface ITradeOfferStore
{
    Task CreateAsync(Guid sellOrderId, ulong tradeOfferId, string steamId64, CancellationToken ct = default);

    Task<IReadOnlyList<TradeOfferRecord>> GetActiveAsync(CancellationToken ct = default);

    Task UpdateStatusAsync(Guid id, int status, CancellationToken ct = default);

    Task<TradeOfferRecord?> GetBySellOrderIdAsync(Guid sellOrderId, CancellationToken ct = default);
}
