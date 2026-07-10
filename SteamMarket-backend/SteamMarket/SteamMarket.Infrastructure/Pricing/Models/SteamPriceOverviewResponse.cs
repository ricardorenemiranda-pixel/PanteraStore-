using System.Text.Json.Serialization;

namespace SteamMarket.Infrastructure.Pricing.Models;

/// <summary>
/// Respuesta cruda de https://steamcommunity.com/market/priceoverview/.
/// Los precios vienen como texto formateado (ej. "$0.03"), no como numero.
/// </summary>
internal sealed class SteamPriceOverviewResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("lowest_price")]
    public string? LowestPrice { get; set; }

    [JsonPropertyName("median_price")]
    public string? MedianPrice { get; set; }

    [JsonPropertyName("volume")]
    public string? Volume { get; set; }
}
