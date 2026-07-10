using System.Text.RegularExpressions;

namespace SteamMarket.Api.Validation;

/// <summary>
/// Formato fijo de una Trade URL de Steam: https://steamcommunity.com/tradeoffer/new/?partner=NUMERO&amp;token=TOKEN
/// Compartido entre UpdateTradeUrlRequestValidator y AddAdminAccountRequestValidator para no
/// duplicar el regex.
/// </summary>
public static partial class TradeUrlFormat
{
    public static bool IsValid(string? url) => !string.IsNullOrWhiteSpace(url) && Pattern().IsMatch(url);

    [GeneratedRegex(@"^https://steamcommunity\.com/tradeoffer/new/\?partner=\d+&token=[A-Za-z0-9_-]+$")]
    private static partial Regex Pattern();
}
