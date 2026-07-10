namespace SteamMarket.Api.Contracts;

/// <summary>Body opcional de POST /api/admin/orders/{id}/reject.</summary>
public sealed class RejectOrderRequest
{
    public string? Note { get; init; }
}
