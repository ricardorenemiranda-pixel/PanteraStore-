namespace SteamMarket.Application.Common.Interfaces;

public sealed class TradeOfferRecord
{
    public Guid Id { get; set; }

    /// <summary>"SellOrder" | "LootBoxRedeem".</summary>
    public string Kind { get; set; } = "SellOrder";

    public Guid? SellOrderId { get; set; }
    public Guid? LootBoxWinId { get; set; }

    public ulong TradeOfferId { get; set; }
    public string SteamId64 { get; set; } = string.Empty;

    /// <summary>Ultimo estado conocido (numero de BotTradeOfferState), actualizado por el polling.</summary>
    public int Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastCheckedAtUtc { get; set; }
}

/// <summary>Persistencia de las ofertas de intercambio enviadas por el bot (en cualquiera de
/// sus dos direcciones, ver TradeOffer), para poder consultarlas periodicamente y saber cuando
/// ya se pueden dar por resueltas.</summary>
public interface ITradeOfferStore
{
    /// <summary>Oferta donde el bot le PIDE items al usuario (venta confirmada).</summary>
    Task CreateAsync(Guid sellOrderId, ulong tradeOfferId, string steamId64, CancellationToken ct = default);

    /// <summary>Oferta donde el bot le ENTREGA un item ganado al usuario (canje de caja).</summary>
    Task CreateForLootBoxRedeemAsync(Guid lootBoxWinId, ulong tradeOfferId, string steamId64, CancellationToken ct = default);

    Task<IReadOnlyList<TradeOfferRecord>> GetActiveAsync(CancellationToken ct = default);

    Task UpdateStatusAsync(Guid id, int status, CancellationToken ct = default);

    Task<TradeOfferRecord?> GetBySellOrderIdAsync(Guid sellOrderId, CancellationToken ct = default);

    Task<TradeOfferRecord?> GetByLootBoxWinIdAsync(Guid lootBoxWinId, CancellationToken ct = default);
}
