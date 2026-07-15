using Microsoft.EntityFrameworkCore;
using SteamMarket.Application.Common.Interfaces;

namespace SteamMarket.Infrastructure.Persistence;

public sealed class EfLootBoxStore : ILootBoxStore
{
    private readonly SteamMarketDbContext _db;

    public EfLootBoxStore(SteamMarketDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<LootBoxRecord>> GetActiveBoxesAsync(CancellationToken ct = default)
    {
        var rows = await _db.LootBoxes.Where(b => b.IsActive).ToListAsync(ct);
        return rows.Select(ToRecord).ToList();
    }

    public async Task<IReadOnlyList<LootBoxRecord>> GetAllBoxesAsync(CancellationToken ct = default)
    {
        var rows = await _db.LootBoxes.ToListAsync(ct);
        return rows.Select(ToRecord).ToList();
    }

    public async Task<LootBoxRecord?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var row = await _db.LootBoxes.FirstOrDefaultAsync(b => b.Slug == slug, ct);
        return row is null ? null : ToRecord(row);
    }

    public async Task<LootBoxRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var row = await _db.LootBoxes.FirstOrDefaultAsync(b => b.Id == id, ct);
        return row is null ? null : ToRecord(row);
    }

    public async Task<LootBoxRecord> UpsertBoxAsync(
        string slug, string name, string category, decimal price, decimal? maxItemPrice, string? imageUrl,
        int sortOrder, bool isActive, CancellationToken ct = default)
    {
        var row = await _db.LootBoxes.FirstOrDefaultAsync(b => b.Slug == slug, ct);

        if (row is null)
        {
            row = new LootBox { Id = Guid.NewGuid(), Slug = slug };
            _db.LootBoxes.Add(row);
        }

        row.Name = name;
        row.Category = category;
        row.Price = price;
        row.MaxItemPrice = maxItemPrice;
        row.ImageUrl = imageUrl;
        row.SortOrder = sortOrder;
        row.IsActive = isActive;

        await _db.SaveChangesAsync(ct);
        return ToRecord(row);
    }

    public async Task<IReadOnlyList<LootBoxPoolItemRecord>> GetPoolItemsAsync(Guid lootBoxId, CancellationToken ct = default)
    {
        var rows = await _db.LootBoxPoolItems.Where(p => p.LootBoxId == lootBoxId).ToListAsync(ct);
        return rows.Select(ToRecord).ToList();
    }

    public async Task<LootBoxPoolItemRecord?> GetPoolItemByIdAsync(Guid poolItemId, CancellationToken ct = default)
    {
        var row = await _db.LootBoxPoolItems.FirstOrDefaultAsync(p => p.Id == poolItemId, ct);
        return row is null ? null : ToRecord(row);
    }

    public async Task<LootBoxPoolItemRecord> AddPoolItemAsync(
        Guid lootBoxId, string marketHashName, string displayName, string? hero, string? slot,
        string type, string rarity, string? imageUrl, int weight, CancellationToken ct = default)
    {
        var row = new LootBoxPoolItem
        {
            Id = Guid.NewGuid(),
            LootBoxId = lootBoxId,
            MarketHashName = marketHashName,
            DisplayName = displayName,
            Hero = hero,
            Slot = slot,
            Type = type,
            Rarity = rarity,
            ImageUrl = imageUrl,
            Weight = weight,
        };

        _db.LootBoxPoolItems.Add(row);
        await _db.SaveChangesAsync(ct);
        return ToRecord(row);
    }

    public async Task RemovePoolItemAsync(Guid poolItemId, CancellationToken ct = default)
    {
        var row = await _db.LootBoxPoolItems.FirstOrDefaultAsync(p => p.Id == poolItemId, ct);
        if (row is null) return;

        _db.LootBoxPoolItems.Remove(row);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> GetPurchaseCountAsync(string steamId64, Guid lootBoxId, CancellationToken ct = default)
    {
        var row = await _db.LootBoxPurchaseCounters
            .FirstOrDefaultAsync(c => c.SteamId64 == steamId64 && c.LootBoxId == lootBoxId, ct);

        return row?.Count ?? 0;
    }

    public async Task IncrementPurchaseCountAsync(string steamId64, Guid lootBoxId, CancellationToken ct = default)
    {
        var row = await _db.LootBoxPurchaseCounters
            .FirstOrDefaultAsync(c => c.SteamId64 == steamId64 && c.LootBoxId == lootBoxId, ct);

        if (row is null)
        {
            row = new LootBoxPurchaseCounter { Id = Guid.NewGuid(), SteamId64 = steamId64, LootBoxId = lootBoxId, Count = 0 };
            _db.LootBoxPurchaseCounters.Add(row);
        }

        row.Count++;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ResetPurchaseCountAsync(string steamId64, Guid lootBoxId, CancellationToken ct = default)
    {
        var row = await _db.LootBoxPurchaseCounters
            .FirstOrDefaultAsync(c => c.SteamId64 == steamId64 && c.LootBoxId == lootBoxId, ct);

        if (row is null) return;

        row.Count = 0;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<LootBoxWinRecord?> TryCreateWinAsync(
        Guid lootBoxId, Guid poolItemId, string steamId64, string botAssetId, CancellationToken ct = default)
    {
        var row = new LootBoxWin
        {
            Id = Guid.NewGuid(),
            LootBoxId = lootBoxId,
            PoolItemId = poolItemId,
            SteamId64 = steamId64,
            BotAssetId = botAssetId,
            Status = "Reserved",
            WonAtUtc = DateTime.UtcNow,
        };

        _db.LootBoxWins.Add(row);

        try
        {
            await _db.SaveChangesAsync(ct);
            return ToRecord(row);
        }
        catch (DbUpdateException)
        {
            // Choco contra el indice unico de BotAssetId: otra apertura concurrente ya se quedo
            // con ese mismo item. El caller (LootBoxService) reintenta con otro asset.
            _db.Entry(row).State = EntityState.Detached;
            return null;
        }
    }

    public async Task<LootBoxWinRecord?> GetWinByIdAsync(Guid id, CancellationToken ct = default)
    {
        var row = await _db.LootBoxWins.FirstOrDefaultAsync(w => w.Id == id, ct);
        return row is null ? null : ToRecord(row);
    }

    public async Task<IReadOnlyList<LootBoxWinRecord>> GetWinsByUserAsync(string steamId64, CancellationToken ct = default)
    {
        var rows = await _db.LootBoxWins.Where(w => w.SteamId64 == steamId64).ToListAsync(ct);
        return rows.Select(ToRecord).ToList();
    }

    public async Task<IReadOnlyList<string>> GetActivelyClaimedBotAssetIdsAsync(CancellationToken ct = default)
    {
        var activeStatuses = new[] { "Reserved", "PendingRedeem" };
        return await _db.LootBoxWins
            .Where(w => activeStatuses.Contains(w.Status))
            .Select(w => w.BotAssetId)
            .ToListAsync(ct);
    }

    public async Task UpdateWinStatusAsync(Guid id, string status, DateTime? resolvedAtUtc, CancellationToken ct = default)
    {
        var row = await _db.LootBoxWins.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (row is null) return;

        row.Status = status;
        row.ResolvedAtUtc = resolvedAtUtc;
        await _db.SaveChangesAsync(ct);
    }

    private static LootBoxRecord ToRecord(LootBox b) => new()
    {
        Id = b.Id,
        Slug = b.Slug,
        Name = b.Name,
        Category = b.Category,
        Price = b.Price,
        MaxItemPrice = b.MaxItemPrice,
        ImageUrl = b.ImageUrl,
        SortOrder = b.SortOrder,
        IsActive = b.IsActive,
    };

    private static LootBoxPoolItemRecord ToRecord(LootBoxPoolItem p) => new()
    {
        Id = p.Id,
        LootBoxId = p.LootBoxId,
        MarketHashName = p.MarketHashName,
        DisplayName = p.DisplayName,
        Hero = p.Hero,
        Slot = p.Slot,
        Type = p.Type,
        Rarity = p.Rarity,
        ImageUrl = p.ImageUrl,
        Weight = p.Weight,
    };

    private static LootBoxWinRecord ToRecord(LootBoxWin w) => new()
    {
        Id = w.Id,
        LootBoxId = w.LootBoxId,
        PoolItemId = w.PoolItemId,
        SteamId64 = w.SteamId64,
        BotAssetId = w.BotAssetId,
        Status = w.Status,
        WonAtUtc = w.WonAtUtc,
        ResolvedAtUtc = w.ResolvedAtUtc,
    };
}
