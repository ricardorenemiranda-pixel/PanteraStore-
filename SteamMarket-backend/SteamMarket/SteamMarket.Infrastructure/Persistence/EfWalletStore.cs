using Microsoft.EntityFrameworkCore;
using SteamMarket.Application.Common.Interfaces;

namespace SteamMarket.Infrastructure.Persistence;

/// <summary>Implementa IWalletStore (Application) contra SQLite: UserProfiles.Balance (cache) + WalletTransactions (libro mayor).</summary>
public sealed class EfWalletStore : IWalletStore
{
    private readonly SteamMarketDbContext _db;

    public EfWalletStore(SteamMarketDbContext db) => _db = db;

    public async Task<decimal> GetBalanceAsync(string steamId64, CancellationToken ct = default)
    {
        var profile = await _db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.SteamId64 == steamId64, ct);
        return profile?.Balance ?? 0m;
    }

    public async Task<IReadOnlyList<WalletTransactionRecord>> GetTransactionsAsync(string steamId64, CancellationToken ct = default)
    {
        return await _db.WalletTransactions.AsNoTracking()
            .Where(t => t.SteamId64 == steamId64)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => new WalletTransactionRecord
            {
                Id = t.Id,
                Amount = t.Amount,
                Type = t.Type,
                Description = t.Description,
                CreatedAtUtc = t.CreatedAtUtc
            })
            .ToListAsync(ct);
    }

    public async Task CreditAsync(string steamId64, decimal amount, string type, string? relatedId, string? description, CancellationToken ct = default)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "El credito debe ser mayor a 0.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var profile = await GetOrCreateProfileAsync(steamId64, ct);
        profile.Balance += amount;
        profile.UpdatedAtUtc = DateTime.UtcNow;

        _db.WalletTransactions.Add(new WalletTransaction
        {
            Id = Guid.NewGuid(),
            SteamId64 = steamId64,
            Amount = amount,
            Type = type,
            RelatedId = relatedId,
            Description = description,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<bool> TryDebitAsync(string steamId64, decimal amount, string type, string? relatedId, string? description, CancellationToken ct = default)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "El debito debe ser mayor a 0.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.SteamId64 == steamId64, ct);
        if (profile is null || profile.Balance < amount)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        profile.Balance -= amount;
        profile.UpdatedAtUtc = DateTime.UtcNow;

        _db.WalletTransactions.Add(new WalletTransaction
        {
            Id = Guid.NewGuid(),
            SteamId64 = steamId64,
            Amount = -amount,
            Type = type,
            RelatedId = relatedId,
            Description = description,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<WithdrawalRecord> CreateWithdrawalAsync(string steamId64, decimal amount, string method, string destination, CancellationToken ct = default)
    {
        var entity = new WithdrawalRequest
        {
            Id = Guid.NewGuid(),
            SteamId64 = steamId64,
            Amount = amount,
            Method = method,
            Destination = destination,
            Status = "Pending",
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.WithdrawalRequests.Add(entity);
        await _db.SaveChangesAsync(ct);

        return ToRecord(entity);
    }

    public async Task<IReadOnlyList<WithdrawalRecord>> GetWithdrawalsAsync(string steamId64, CancellationToken ct = default)
    {
        var entities = await _db.WithdrawalRequests.AsNoTracking()
            .Where(w => w.SteamId64 == steamId64)
            .OrderByDescending(w => w.CreatedAtUtc)
            .ToListAsync(ct);

        return entities.Select(ToRecord).ToList();
    }

    public async Task<IReadOnlyList<WithdrawalRecord>> GetPendingWithdrawalsAsync(CancellationToken ct = default)
    {
        var entities = await _db.WithdrawalRequests.AsNoTracking()
            .Where(w => w.Status == "Pending")
            .OrderBy(w => w.CreatedAtUtc)
            .ToListAsync(ct);

        return entities.Select(ToRecord).ToList();
    }

    public async Task<WithdrawalRecord?> GetWithdrawalByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.WithdrawalRequests.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task UpdateWithdrawalStatusAsync(Guid id, string status, CancellationToken ct = default)
    {
        var entity = await _db.WithdrawalRequests.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (entity is null) return;

        entity.Status = status;
        entity.ResolvedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<UserProfile> GetOrCreateProfileAsync(string steamId64, CancellationToken ct)
    {
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.SteamId64 == steamId64, ct);
        if (profile is null)
        {
            profile = new UserProfile { SteamId64 = steamId64 };
            _db.UserProfiles.Add(profile);
        }
        return profile;
    }

    private static WithdrawalRecord ToRecord(WithdrawalRequest w) => new()
    {
        Id = w.Id,
        SteamId64 = w.SteamId64,
        Amount = w.Amount,
        Method = w.Method,
        Destination = w.Destination,
        Status = w.Status,
        CreatedAtUtc = w.CreatedAtUtc,
        ResolvedAtUtc = w.ResolvedAtUtc
    };
}
