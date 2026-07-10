using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SteamMarket.Application.Common.Interfaces;

namespace SteamMarket.Infrastructure.Persistence;

/// <summary>Implementa ISellOrderStore (Application) contra la tabla SellOrders en SQLite.</summary>
public sealed class EfSellOrderStore : ISellOrderStore
{
    private readonly SteamMarketDbContext _db;

    public EfSellOrderStore(SteamMarketDbContext db) => _db = db;

    public async Task<SellOrderRecord> CreateAsync(string steamId64, IReadOnlyList<SellOrderItem> items, decimal totalAmount, CancellationToken ct = default)
    {
        var entity = new SellOrder
        {
            Id = Guid.NewGuid(),
            SteamId64 = steamId64,
            ItemsJson = JsonSerializer.Serialize(items),
            TotalAmount = totalAmount,
            Status = "Pending",
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.SellOrders.Add(entity);
        await _db.SaveChangesAsync(ct);

        return ToRecord(entity);
    }

    public async Task<IReadOnlyList<SellOrderRecord>> GetByUserAsync(string steamId64, CancellationToken ct = default)
    {
        var entities = await _db.SellOrders.AsNoTracking()
            .Where(o => o.SteamId64 == steamId64)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(ct);

        return entities.Select(ToRecord).ToList();
    }

    public async Task<IReadOnlyList<SellOrderRecord>> GetPendingAsync(CancellationToken ct = default)
    {
        var entities = await _db.SellOrders.AsNoTracking()
            .Where(o => o.Status == "Pending")
            .OrderBy(o => o.CreatedAtUtc)
            .ToListAsync(ct);

        return entities.Select(ToRecord).ToList();
    }

    public async Task<SellOrderRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.SellOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task UpdateStatusAsync(Guid id, string status, string? adminNote, CancellationToken ct = default)
    {
        var entity = await _db.SellOrders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (entity is null) return;

        entity.Status = status;
        entity.AdminNote = adminNote;
        entity.ResolvedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static SellOrderRecord ToRecord(SellOrder o) => new()
    {
        Id = o.Id,
        SteamId64 = o.SteamId64,
        Items = JsonSerializer.Deserialize<List<SellOrderItem>>(o.ItemsJson) ?? new List<SellOrderItem>(),
        TotalAmount = o.TotalAmount,
        Status = o.Status,
        CreatedAtUtc = o.CreatedAtUtc,
        ResolvedAtUtc = o.ResolvedAtUtc,
        AdminNote = o.AdminNote
    };
}
