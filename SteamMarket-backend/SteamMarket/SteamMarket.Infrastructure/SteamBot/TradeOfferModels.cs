using System.Text.Json.Serialization;

namespace SteamMarket.Infrastructure.SteamBot;

/// <summary>
/// Forma del JSON que espera steamcommunity.com/tradeoffer/new/send en el campo
/// "json_tradeoffer" (endpoint no documentado oficialmente por Valve, pero estable y usado por
/// practicamente todos los bots de intercambio de items). "me" = la cuenta bot, "them" = el
/// usuario al que le pedimos los items.
/// </summary>
public sealed class TradeOfferJson
{
    [JsonPropertyName("newversion")]
    public bool NewVersion { get; set; } = true;

    [JsonPropertyName("version")]
    public int Version { get; set; } = 2;

    [JsonPropertyName("me")]
    public TradeOfferSide Me { get; set; } = new();

    [JsonPropertyName("them")]
    public TradeOfferSide Them { get; set; } = new();
}

public sealed class TradeOfferSide
{
    [JsonPropertyName("assets")]
    public List<TradeOfferAsset> Assets { get; set; } = new();

    [JsonPropertyName("currency")]
    public List<object> Currency { get; set; } = new();

    [JsonPropertyName("ready")]
    public bool Ready { get; set; }
}

public sealed class TradeOfferAsset
{
    [JsonPropertyName("appid")]
    public int AppId { get; set; } = 570; // Dota 2

    [JsonPropertyName("contextid")]
    public string ContextId { get; set; } = "2"; // contexto de items de Dota 2

    [JsonPropertyName("amount")]
    public int Amount { get; set; } = 1;

    [JsonPropertyName("assetid")]
    public string AssetId { get; set; } = string.Empty;
}

/// <summary>Va en el campo "trade_offer_create_params": el token que trae la Trade URL del
/// destinatario, necesario para mandarle una oferta sin ser su amigo en Steam.</summary>
public sealed class TradeOfferCreateParams
{
    [JsonPropertyName("trade_offer_access_token")]
    public string TradeOfferAccessToken { get; set; } = string.Empty;
}
