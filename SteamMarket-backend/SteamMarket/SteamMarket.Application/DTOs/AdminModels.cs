namespace SteamMarket.Application.DTOs;

public sealed class AdminAccountDto
{
    public string SteamId { get; init; } = string.Empty;
    public string TradeUrl { get; init; } = string.Empty;
    public string? Label { get; init; }

    /// <summary>true = es el admin fijo de appsettings/user-secrets (no se puede quitar desde la UI).</summary>
    public bool IsSuperAdmin { get; init; }

    public DateTime? AddedAtUtc { get; init; }
}
