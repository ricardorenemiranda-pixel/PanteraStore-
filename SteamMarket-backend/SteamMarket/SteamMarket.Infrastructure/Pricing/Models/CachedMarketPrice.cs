namespace SteamMarket.Infrastructure.Pricing.Models;

/// <summary>
/// Fila de la cache propia de precios. Esto NO es una entidad de dominio:
/// es un detalle tecnico de Infrastructure para no golpear a Steam en cada request
/// (el priceoverview publico tiene rate limits fuertes).
/// </summary>
public sealed class CachedMarketPrice
{
    public string MarketHashName { get; set; } = string.Empty; // PK
    public decimal Price { get; set; }
    public DateTime FetchedAtUtc { get; set; }

    // Con que moneda de Steam se pidio este precio (ej. 26 = PEN, 1 = USD). Si mas adelante
    // se cambia Pricing:CurrencyId, las filas viejas quedan con el CurrencyId anterior y
    // dejan de matchear -> se piden de nuevo en la moneda correcta en vez de mostrarse
    // disfrazadas de la moneda nueva.
    public int CurrencyId { get; set; }
}
