using System.Text.Json.Serialization;

namespace SteamMarket.Infrastructure.Steam.Models;

// Mapean el JSON crudo del endpoint publico de Steam:
// https://steamcommunity.com/inventory/{steamid}/570/2
// OJO: varios campos numericos vienen como string.

internal sealed class SteamInventoryResponse
{
    [JsonPropertyName("assets")]
    public List<SteamAsset>? Assets { get; set; }

    [JsonPropertyName("descriptions")]
    public List<SteamDescription>? Descriptions { get; set; }

    [JsonPropertyName("success")]
    public int Success { get; set; }

    // Paginacion: Steam corta la respuesta en paginas de "count" items.
    // Si more_items = 1, hay que repetir el pedido con start_assetid = last_assetid.
    [JsonPropertyName("more_items")]
    public int MoreItems { get; set; }

    [JsonPropertyName("last_assetid")]
    public string? LastAssetId { get; set; }
}

internal sealed class SteamAsset
{
    [JsonPropertyName("assetid")]
    public string AssetId { get; set; } = string.Empty;

    [JsonPropertyName("classid")]
    public string ClassId { get; set; } = string.Empty;

    [JsonPropertyName("instanceid")]
    public string InstanceId { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public string Amount { get; set; } = "1";
}

internal sealed class SteamDescription
{
    [JsonPropertyName("classid")]
    public string ClassId { get; set; } = string.Empty;

    [JsonPropertyName("instanceid")]
    public string InstanceId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("market_name")]
    public string? MarketName { get; set; }

    [JsonPropertyName("market_hash_name")]
    public string? MarketHashName { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("icon_url")]
    public string? IconUrl { get; set; }

    [JsonPropertyName("tradable")]
    public int Tradable { get; set; }

    [JsonPropertyName("marketable")]
    public int Marketable { get; set; }

    // Aca viene la rareza (categoria "Rarity", ej. "Immortal", "Mythical") y la
    // calidad (categoria "Quality", ej. "Arcana", "Genuine", "Unusual").
    [JsonPropertyName("tags")]
    public List<SteamTag>? Tags { get; set; }
}

internal sealed class SteamTag
{
    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("internal_name")]
    public string? InternalName { get; set; }

    [JsonPropertyName("localized_tag_name")]
    public string? LocalizedTagName { get; set; }

    // Color hex (sin '#') que Steam asigna a la rareza, ej. "8847ff" para Mythical.
    [JsonPropertyName("color")]
    public string? Color { get; set; }
}
