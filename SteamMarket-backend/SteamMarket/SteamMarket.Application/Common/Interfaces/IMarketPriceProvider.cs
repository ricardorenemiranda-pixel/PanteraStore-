namespace SteamMarket.Application.Common.Interfaces;

/// <summary>
/// Contrato (puerto) para obtener precios de referencia del mercado.
/// La capa Application lo DEFINE; la capa Infrastructure lo IMPLEMENTA
/// (llamando a Steam Market y/o leyendo de una cache propia).
/// </summary>
public interface IMarketPriceProvider
{
    /// <summary>
    /// Devuelve un precio por cada market_hash_name pedido (cuando se pudo obtener).
    /// Se pide en batch a proposito: permite deduplicar nombres repetidos y
    /// que la implementacion controle el throttling contra el rate limit de Steam.
    /// </summary>
    /// <param name="force">
    /// Si es true, ignora el cache de precios (SQLite) y vuelve a pedirle a Steam aunque el
    /// precio guardado todavia este "fresco". Lo usa el boton "Actualizar" del frontend.
    /// </param>
    /// <param name="maxLiveFetches">
    /// Cuantos precios NUEVOS (no cacheados) se le piden a Steam como maximo en esta llamada.
    /// El resto queda sin cotizar por ahora (no es error). Sirve para que un caller "interactivo"
    /// (un usuario esperando la pagina) no se quede pegado si hay muchos items sin precio; un
    /// caller en segundo plano (sin apuro) puede pasar un numero mas alto. Null = usa el default
    /// interno del proveedor.
    /// </param>
    Task<IReadOnlyDictionary<string, MarketPriceResult>> GetPricesAsync(
        IEnumerable<string> marketHashNames,
        bool force = false,
        int? maxLiveFetches = null,
        CancellationToken ct = default);
}

/// <summary>
/// Resultado de cotizar un item individual.
/// FromCache indica si vino de la base de datos propia en vez de Steam en vivo.
/// </summary>
public sealed record MarketPriceResult(bool Success, decimal? Price, bool FromCache, string? Error);
