using SteamMarket.Domain.Entities;

namespace SteamMarket.Application.DTOs;

/// <summary>
/// Lo que la API le devuelve al frontend. Es un DTO plano, sin logica.
/// </summary>
public sealed class InventoryItemDto
{
    public string AssetId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string MarketHashName { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool Tradable { get; init; }
    public bool Marketable { get; init; }
    public string? IconUrl { get; init; }
    public decimal? MarketPrice { get; init; }
    public decimal? PayoutPrice { get; init; }
    public string? Rarity { get; init; }
    public string? Quality { get; init; }
    public string? RarityColor { get; init; }
    public string? Hero { get; init; }

    public static InventoryItemDto FromDomain(InventoryItem item) => new()
    {
        AssetId = item.AssetId,
        Name = item.Name,
        MarketHashName = item.MarketHashName,
        Type = item.Type,
        Tradable = item.Tradable,
        Marketable = item.Marketable,
        IconUrl = item.IconUrl,
        MarketPrice = item.MarketPrice,
        PayoutPrice = item.PayoutPrice,
        Rarity = item.Rarity,
        Quality = item.Quality,
        RarityColor = item.RarityColor,
        Hero = item.Hero
    };
}

public sealed class InventoryResponse
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<InventoryItemDto> Items { get; init; } = Array.Empty<InventoryItemDto>();
    public int Count => Items.Count;

    /// <summary>Cuando se obtuvo este inventario (util para mostrar "actualizado hace X min" en el frontend).</summary>
    public DateTime? FetchedAtUtc { get; init; }

    public static InventoryResponse Ok(IReadOnlyList<InventoryItemDto> items, DateTime? fetchedAtUtc = null) =>
        new() { Success = true, Items = items, FetchedAtUtc = fetchedAtUtc };

    public static InventoryResponse Fail(string error) =>
        new() { Success = false, Error = error };
}
