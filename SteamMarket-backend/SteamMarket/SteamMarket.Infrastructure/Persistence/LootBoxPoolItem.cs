namespace SteamMarket.Infrastructure.Persistence;

/// <summary>
/// Un posible resultado al abrir una LootBox: es una entrada del CATALOGO ("esta caja puede
/// darte un Manifold Paradox"), no un item fisico especifico todavia (eso es
/// LootBoxWin.BotAssetId). Al abrir la caja de verdad, LootBoxService busca stock real en el
/// inventario de Steam de la cuenta bot que coincida con MarketHashName; si no hay, ese item
/// queda afuera del sorteo hasta que el admin lo reponga.
/// </summary>
public sealed class LootBoxPoolItem
{
    public Guid Id { get; set; }
    public Guid LootBoxId { get; set; }

    public string MarketHashName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Hero { get; set; }

    /// <summary>Weapon, Head, Shoulder, Arms, Back, Legs, Tail, Armor, Off-Hand, Ability, N/A...</summary>
    public string? Slot { get; set; }

    /// <summary>"Wearable" | "Bundle".</summary>
    public string Type { get; set; } = "Wearable";

    /// <summary>Arcana, Immortal, Legendary, Mythical, etc. (igual que InventoryItem.Rarity).</summary>
    public string Rarity { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    /// <summary>Peso relativo para el sorteo (mayor = mas probable). Default 1 = todos iguales.</summary>
    public int Weight { get; set; } = 1;
}
