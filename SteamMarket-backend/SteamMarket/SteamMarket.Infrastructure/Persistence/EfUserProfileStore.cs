using Microsoft.EntityFrameworkCore;
using SteamMarket.Application.Common.Interfaces;

namespace SteamMarket.Infrastructure.Persistence;

/// <summary>Implementa IUserProfileStore (Application) contra la tabla UserProfiles en SQLite.</summary>
public sealed class EfUserProfileStore : IUserProfileStore
{
    private readonly SteamMarketDbContext _db;

    public EfUserProfileStore(SteamMarketDbContext db) => _db = db;

    public async Task<StoredProfile?> GetAsync(string steamId64, CancellationToken ct = default)
    {
        var p = await _db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.SteamId64 == steamId64, ct);
        if (p is null) return null;

        return new StoredProfile
        {
            TradeUrl = p.TradeUrl,
            FirstName = p.FirstName,
            LastName = p.LastName,
            Email = p.Email,
            Phone = p.Phone,
            DocumentType = p.DocumentType,
            DocumentNumber = p.DocumentNumber
        };
    }

    public async Task SetTradeUrlAsync(string steamId64, string tradeUrl, CancellationToken ct = default)
    {
        var profile = await GetOrCreateAsync(steamId64, ct);
        profile.TradeUrl = tradeUrl;
        profile.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SavePersonalDataAsync(string steamId64, PersonalDataInput data, CancellationToken ct = default)
    {
        var profile = await GetOrCreateAsync(steamId64, ct);
        profile.FirstName = data.FirstName;
        profile.LastName = data.LastName;
        profile.Email = data.Email;
        profile.Phone = data.Phone;
        profile.DocumentType = data.DocumentType;
        profile.DocumentNumber = data.DocumentNumber;
        profile.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ClearPersonalDataAsync(string steamId64, CancellationToken ct = default)
    {
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(x => x.SteamId64 == steamId64, ct);
        if (profile is null) return; // nunca guardo nada, no hay nada que borrar

        profile.FirstName = null;
        profile.LastName = null;
        profile.Email = null;
        profile.Phone = null;
        profile.DocumentType = null;
        profile.DocumentNumber = null;
        profile.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<UserProfile> GetOrCreateAsync(string steamId64, CancellationToken ct)
    {
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(x => x.SteamId64 == steamId64, ct);
        if (profile is null)
        {
            profile = new UserProfile { SteamId64 = steamId64 };
            _db.UserProfiles.Add(profile);
        }
        return profile;
    }
}
