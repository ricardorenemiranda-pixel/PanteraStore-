namespace SteamMarket.Api.Contracts;

/// <summary>Body de POST /api/admin/accounts.</summary>
public sealed class AddAdminAccountRequest
{
    public string SteamId64 { get; init; } = string.Empty;
    public string TradeUrl { get; init; } = string.Empty;
    public string? Label { get; init; }
}
