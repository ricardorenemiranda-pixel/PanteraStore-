using SteamMarket.Domain.Entities;

namespace SteamMarket.Application.Common.Interfaces;

/// <summary>
/// Contrato (puerto) para obtener el inventario de Steam.
/// La capa Application lo DEFINE; la capa Infrastructure lo IMPLEMENTA.
/// Asi Application no sabe nada de HTTP, JSON ni de Steam directamente.
/// </summary>
public interface ISteamInventoryClient
{
    /// <param name="force">
    /// Si es true, ignora el cache "fresco" y fuerza un pedido en vivo a Steam (el usuario
    /// apreto "Actualizar"). Igual respeta el throttle global y el fallback a cache vencido
    /// si Steam falla.
    /// </param>
    Task<SteamFetchResult> GetDotaInventoryAsync(string steamId64, bool force = false, CancellationToken ct = default);
}

/// <summary>
/// Resultado de traer el inventario desde Steam (con exito o con error controlado).
/// </summary>
public sealed class SteamFetchResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<InventoryItem> Items { get; init; } = Array.Empty<InventoryItem>();

    /// <summary>Cuando se obtuvo este dato (de Steam en vivo, o cuando se guardo en cache).</summary>
    public DateTime? FetchedAtUtc { get; init; }

    public static SteamFetchResult Ok(IReadOnlyList<InventoryItem> items, DateTime? fetchedAtUtc = null) =>
        new() { Success = true, Items = items, FetchedAtUtc = fetchedAtUtc };

    public static SteamFetchResult Fail(string error) =>
        new() { Success = false, Error = error };
}
