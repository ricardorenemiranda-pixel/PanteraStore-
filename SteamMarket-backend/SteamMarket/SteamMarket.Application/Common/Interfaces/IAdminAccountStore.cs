namespace SteamMarket.Application.Common.Interfaces;

public sealed class AdminAccountRecord
{
    public string SteamId64 { get; init; } = string.Empty;
    public string TradeUrl { get; init; } = string.Empty;
    public string? Label { get; init; }
    public DateTime AddedAtUtc { get; init; }
}

/// <summary>Puerto hacia la lista de cuentas admin adicionales (aparte del super admin de config).</summary>
public interface IAdminAccountStore
{
    Task<IReadOnlyList<AdminAccountRecord>> GetAllAsync(CancellationToken ct = default);

    Task<bool> ExistsAsync(string steamId64, CancellationToken ct = default);

    Task UpsertAsync(string steamId64, string tradeUrl, string? label, CancellationToken ct = default);

    Task RemoveAsync(string steamId64, CancellationToken ct = default);
}
