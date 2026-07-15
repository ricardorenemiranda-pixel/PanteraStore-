namespace SteamMarket.Infrastructure.Persistence;

/// <summary>
/// Una "caja" (loot box) que el usuario puede comprar para intentar ganar un item de Dota al
/// azar, igual que las cajas de GiorDota (giordota.com/cajas). El contenido posible de cada
/// caja vive en LootBoxPoolItem; el premio real que le toca a un usuario en LootBoxWin.
/// </summary>
public sealed class LootBox
{
    public Guid Id { get; set; }

    /// <summary>Usado en la URL (/cajas/{Slug}), ej. "carry".</summary>
    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>"Rol" | "VIP" | "Medalla" | "Gratis" (secciones de la pagina /cajas).</summary>
    public string Category { get; set; } = string.Empty;

    public decimal Price { get; set; }

    /// <summary>Tope de valor de mercado que puede tener un item para poder agregarse al pool de
    /// esta caja (ver LootBoxService.AddPoolItemFromStockAsync). Null = sin limite.</summary>
    public decimal? MaxItemPrice { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary>Orden de aparicion dentro de su categoria.</summary>
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
