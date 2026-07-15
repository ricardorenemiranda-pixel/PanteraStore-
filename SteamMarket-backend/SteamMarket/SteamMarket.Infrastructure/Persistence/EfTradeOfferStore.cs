using Microsoft.EntityFrameworkCore;
using SteamMarket.Application.Common.Interfaces;

namespace SteamMarket.Infrastructure.Persistence;

public sealed class EfTradeOfferStore : ITradeOfferStore
{
    private readonly SteamMarketDbContext _db;

    public EfTradeOfferStore(SteamMarketDbContext db)
    {
        _db = db;
    }

    public async Task CreateAsync(Guid sellOrderId, ulong tradeOfferId, string steamId64, CancellationToken ct = default)
    {
        _db.TradeOffers.Add(new TradeOffer
        {
            Id = Guid.NewGuid(),
            Kind = "SellOrder",
            SellOrderId = sellOrderId,
            TradeOfferId = tradeOfferId,
            SteamId64 = steamId64,
            Status = (int)BotTradeOfferState.Active,
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task CreateForLootBoxRedeemAsync(Guid lootBoxWinId, ulong tradeOfferId, string steamId64, CancellationToken ct = default)
    {
        _db.TradeOffers.Add(new TradeOffer
        {
            Id = Guid.NewGuid(),
            Kind = "LootBoxRedeem",
            LootBoxWinId = lootBoxWinId,
            TradeOfferId = tradeOfferId,
            SteamId64 = steamId64,
            Status = (int)BotTradeOfferState.Active,
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TradeOfferRecord>> GetActiveAsync(CancellationToken ct = default)
    {
        var activeStatuses = new[] { (int)BotTradeOfferState.Active, (int)BotTradeOfferState.NeedsConfirmation };

        var rows = await _db.TradeOffers
            .Where(t => activeStatuses.Contains(t.Status))
            .OrderBy(t => t.CreatedAtUtc)
            .ToListAsync(ct);

        return rows.Select(ToRecord).ToList();
    }

    public async Task UpdateStatusAsync(Guid id, int status, CancellationToken ct = default)
    {
        var row = await _db.TradeOffers.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (row is null) return;

        row.Status = status;
        row.LastCheckedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<TradeOfferRecord?> GetBySellOrderIdAsync(Guid sellOrderId, CancellationToken ct = default)
    {
        var row = await _db.TradeOffers.FirstOrDefaultAsync(t => t.SellOrderId == sellOrderId, ct);
        return row is null ? null : ToRecord(row);
    }

    public async Task<TradeOfferRecord?> GetByLootBoxWinIdAsync(Guid lootBoxWinId, CancellationToken ct = default)
    {
        var row = await _db.TradeOffers.FirstOrDefaultAsync(t => t.LootBoxWinId == lootBoxWinId, ct);
        return row is null ? null : ToRecord(row);
    }

    private static TradeOfferRecord ToRecord(TradeOffer t) => new()
    {
        Id = t.Id,
        Kind = t.Kind,
        SellOrderId = t.SellOrderId,
        LootBoxWinId = t.LootBoxWinId,
        TradeOfferId = t.TradeOfferId,
        SteamId64 = t.SteamId64,
        Status = t.Status,
        CreatedAtUtc = t.CreatedAtUtc,
        LastCheckedAtUtc = t.LastCheckedAtUtc,
    };
}
