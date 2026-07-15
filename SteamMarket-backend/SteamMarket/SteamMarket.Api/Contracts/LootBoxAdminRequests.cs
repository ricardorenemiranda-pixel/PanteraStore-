namespace SteamMarket.Api.Contracts;

/// <summary>Body de POST /api/admin/lootboxes: crea o actualiza (por Slug) una caja del catalogo.</summary>
public sealed class CreateOrUpdateLootBoxRequest
{
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    /// <summary>"Rol" | "VIP" | "Medalla" | "Gratis".</summary>
    public string Category { get; init; } = string.Empty;

    public decimal Price { get; init; }

    /// <summary>Tope de valor de mercado que puede tener un item para poder agregarse a esta
    /// caja desde el almacen. Null/0 = sin limite.</summary>
    public decimal? MaxItemPrice { get; init; }

    public string? ImageUrl { get; init; }
    public int SortOrder { get; init; }
    public bool IsActive { get; init; } = true;
}

/// <summary>Body de POST /api/admin/lootboxes/{slug}/items: agrega un item al pool de esa caja
/// tipeando los datos a mano (uso poco frecuente; el flujo normal es AddLootBoxItemFromStockRequest).</summary>
public sealed class AddLootBoxPoolItemRequest
{
    public string MarketHashName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Hero { get; init; }
    public string? Slot { get; init; }
    public string Type { get; init; } = "Wearable";
    public string Rarity { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public int Weight { get; init; } = 1;
}

/// <summary>Body de POST /api/admin/lootboxes/{slug}/items/from-stock: agrega un item que
/// existe de verdad en el almacen (inventario del bot). El backend completa nombre, heroe,
/// tipo, rareza e imagen solo, y calcula el peso (probabilidad) segun el precio actual.</summary>
public sealed class AddLootBoxItemFromStockRequest
{
    public string MarketHashName { get; init; } = string.Empty;
}
