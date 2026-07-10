namespace SteamMarket.Domain.Entities;

/// <summary>
/// Un item del inventario de Dota. Es una entidad del dominio:
/// contiene los datos del item y la regla de negocio de cuanto se le paga al usuario.
/// </summary>
public class InventoryItem
{
    public string AssetId { get; init; } = string.Empty;
    public string ClassId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string MarketHashName { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool Tradable { get; init; }
    public bool Marketable { get; init; }
    public string? IconUrl { get; init; }

    // Rareza (ej. "Immortal", "Mythical", "Legendary") y calidad (ej. "Arcana", "Genuine").
    // En Dota son dos tags distintos en Steam; los guardamos separados porque "Arcana"
    // tecnicamente es una Quality, no una Rarity, aunque coloquialmente se hable de ambas
    // como "rareza" del item.
    public string? Rarity { get; init; }
    public string? Quality { get; init; }
    public string? RarityColor { get; init; }

    // Heroe al que pertenece el item (ej. "Ursa"). Null para items que no son de un heroe
    // especifico (couriers, wards genericos, etc.).
    public string? Hero { get; init; }

    // --- Precios (paso 3) ---
    public decimal? MarketPrice { get; private set; }
    public decimal? PayoutPrice { get; private set; }

    /// <summary>
    /// Regla de negocio: cuanto le pagamos al usuario por su item.
    /// margin = 0.70m significa que pagamos el 70% del precio de mercado.
    /// </summary>
    public void ApplyPricing(decimal marketPrice, decimal margin)
    {
        if (marketPrice < 0) throw new ArgumentOutOfRangeException(nameof(marketPrice));
        if (margin is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(margin));

        MarketPrice = marketPrice;
        PayoutPrice = Math.Round(marketPrice * margin, 2);
    }

    // Los cofres/tesoros SIN ABRIR ("treasure"/"chest"/"cache") SI cuentan como valiosos: un
    // cofre cerrado vale mas que sus piezas por separado, asi que no se excluyen. Solo se
    // excluyen las pantallas de carga (loading screen), que son Immortal pero no son un skin.
    private static readonly string[] ExcludedTypeKeywords = { "loading screen" };

    /// <summary>
    /// Regla de negocio: que se considera "valioso" para este marketplace (lo que se muestra,
    /// se filtra y se cotiza con prioridad). Un solo lugar para este criterio: lo usan tanto
    /// InventoryService (Application) como el warmup de precios en segundo plano (Infrastructure).
    /// </summary>
    public bool IsHighValue()
    {
        var isRareEnough = Rarity is "Immortal" or "Mythical" || Quality == "Arcana";
        if (!isRareEnough) return false;

        var haystack = $"{Type} {Name}".ToLowerInvariant();
        var isExcludedContainer = ExcludedTypeKeywords.Any(k => haystack.Contains(k));
        return !isExcludedContainer;
    }
}
