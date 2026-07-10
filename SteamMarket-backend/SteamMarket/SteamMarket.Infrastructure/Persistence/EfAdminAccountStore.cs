using Microsoft.EntityFrameworkCore;
using SteamMarket.Application.Common.Interfaces;

namespace SteamMarket.Infrastructure.Persistence;

/// <summary>Implementa IAdminAccountStore (Application) contra la tabla AdminAccounts en SQLite.</summary>
public sealed class EfAdminAccountStore : IAdminAccountStore
{
    private readonly SteamMarketDbContext _db;

    public EfAdminAccountStore(SteamMarketDbContext db) => _db = db;

    public async Task<IReadOnlyList<AdminAccountRecord>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.AdminAccounts.AsNoTracking()
            .OrderBy(a => a.AddedAtUtc)
            .Select(a => new AdminAccountRecord
            {
                SteamId64 = a.SteamId64,
                TradeUrl = a.TradeUrl,
                Label = a.Label,
                AddedAtUtc = a.AddedAtUtc
            })
            .ToListAsync(ct);
    }

    public Task<bool> ExistsAsync(string steamId64, CancellationToken ct = default) =>
        _db.AdminAccounts.AsNoTracking().AnyAsync(a => a.SteamId64 == steamId64, ct);

    public async Task UpsertAsync(string steamId64, string tradeUrl, string? label, CancellationToken ct = default)
    {
        var entity = await _db.AdminAccounts.FirstOrDefaultAsync(a => a.SteamId64 == steamId64, ct);
        if (entity is null)
        {
            entity = new AdminAccount { SteamId64 = steamId64, AddedAtUtc = DateTime.UtcNow };
            _db.AdminAccounts.Add(entity);
        }

        entity.TradeUrl = tradeUrl;
        entity.Label = label;

        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(string steamId64, CancellationToken ct = default)
    {
        var entity = await _db.AdminAccounts.FirstOrDefaultAsync(a => a.SteamId64 == steamId64, ct);
        if (entity is null) return;

        _db.AdminAccounts.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }
}
