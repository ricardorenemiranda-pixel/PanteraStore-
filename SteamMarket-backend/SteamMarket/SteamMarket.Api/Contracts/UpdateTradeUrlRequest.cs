namespace SteamMarket.Api.Contracts;

/// <summary>Body de PUT /api/profile/trade-url. Se valida con TradeUrlValidator (FluentValidation).</summary>
public sealed class UpdateTradeUrlRequest
{
    public string TradeUrl { get; init; } = string.Empty;
}
