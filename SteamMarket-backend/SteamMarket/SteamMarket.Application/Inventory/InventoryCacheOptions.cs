using System.ComponentModel.DataAnnotations;

namespace SteamMarket.Application.Inventory;

/// <summary>
/// Configuracion del cache de inventario. Se llena desde appsettings.json (seccion "Inventory").
/// El endpoint de inventario de Steam rate-limita agresivo por IP, asi que servimos el
/// inventario desde nuestra propia base (SQLite) y solo lo re-pedimos a Steam cuando esta
/// "viejo" segun CacheMinutes.
/// </summary>
public sealed class InventoryCacheOptions
{
    /// <summary>Cuantos minutos se considera "fresco" un inventario cacheado antes de volver a pedirlo a Steam.</summary>
    [Range(1, 24 * 60, ErrorMessage = "CacheMinutes debe estar entre 1 minuto y 24 horas.")]
    public int CacheMinutes { get; set; } = 10;
}
