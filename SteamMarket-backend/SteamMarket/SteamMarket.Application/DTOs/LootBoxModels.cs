namespace SteamMarket.Application.DTOs;

public sealed class LootBoxDto
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public decimal Price { get; init; }

    /// <summary>Tope de valor de mercado que puede tener un item para poder agregarse a esta
    /// caja desde el almacen. Null = sin limite.</summary>
    public decimal? MaxItemPrice { get; init; }

    public string? ImageUrl { get; init; }
    public int SortOrder { get; init; }
    public bool IsActive { get; init; }
}

public sealed class LootBoxPoolItemDto
{
    public Guid Id { get; init; }
    public string MarketHashName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Hero { get; init; }
    public string? Slot { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Rarity { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public int Weight { get; init; }
}

public sealed class LootBoxDetailDto
{
    /// <summary>Cuantas compras PAGADAS lleva el usuario en esta caja (0..8). Al llegar a 9 la
    /// siguiente apertura es gratis (ver PityThreshold). 0 si no hay sesion.</summary>
    public const int PityThreshold = 9;

    public LootBoxDto Box { get; init; } = null!;
    public IReadOnlyList<LootBoxPoolItemDto> Contents { get; init; } = Array.Empty<LootBoxPoolItemDto>();
    public int PityCount { get; init; }
}

public sealed class LootBoxWinDto
{
    public Guid Id { get; init; }
    public string BoxName { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public string? ItemImageUrl { get; init; }
    public string Rarity { get; init; } = string.Empty;

    /// <summary>"Reserved" | "PendingRedeem" | "Sold" | "Redeemed".</summary>
    public string Status { get; init; } = string.Empty;

    public DateTime WonAtUtc { get; init; }
    public DateTime? ResolvedAtUtc { get; init; }
}

public sealed class LootBoxOpenResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public LootBoxWinDto? Win { get; init; }
    public bool WasFree { get; init; }
    public int PityCount { get; init; }

    public static LootBoxOpenResult Ok(LootBoxWinDto win, bool wasFree, int pityCount) =>
        new() { Success = true, Win = win, WasFree = wasFree, PityCount = pityCount };

    public static LootBoxOpenResult Fail(string error) => new() { Success = false, Error = error };
}

/// <summary>Resultado de la "PRUEBA GRATIS": una vuelta de preview que NO cobra, NO reserva
/// stock real y NO queda guardada como premio. Solo para mostrar la animacion.</summary>
public sealed class LootBoxDemoResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public LootBoxPoolItemDto? Item { get; init; }

    public static LootBoxDemoResult Ok(LootBoxPoolItemDto item) => new() { Success = true, Item = item };
    public static LootBoxDemoResult Fail(string error) => new() { Success = false, Error = error };
}

public sealed class SellWinResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public decimal? CreditedAmount { get; init; }

    public static SellWinResult Ok(decimal amount) => new() { Success = true, CreditedAmount = amount };
    public static SellWinResult Fail(string error) => new() { Success = false, Error = error };
}

public sealed class RedeemWinResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }

    public static RedeemWinResult Ok() => new() { Success = true };
    public static RedeemWinResult Fail(string error) => new() { Success = false, Error = error };
}

// --- Administracion del catalogo (solo admin) ---

public sealed class LootBoxAdminInput
{
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal? MaxItemPrice { get; init; }
    public string? ImageUrl { get; init; }
    public int SortOrder { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class LootBoxPoolItemInput
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

/// <summary>Un item del inventario REAL de la cuenta bot ("almacen"), cotizado, para que el
/// admin elija cuales agregar al pool de una caja (ver LootBoxService.GetWarehouseAsync /
/// AddPoolItemFromStockAsync). Agrupado por MarketHashName: Quantity es cuantas copias tiene el
/// bot de ese item ahora mismo (sin descontar las ya comprometidas con un premio activo).</summary>
public sealed class WarehouseItemDto
{
    public string MarketHashName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Hero { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Rarity { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public int Quantity { get; init; }

    /// <summary>Precio de mercado actual, si se pudo cotizar.</summary>
    public decimal? Price { get; init; }
}
