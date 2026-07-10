using SteamMarket.Application.DTOs;

namespace SteamMarket.Application.Services.Interfaces;

/// <summary>
/// Contrato del caso de uso de inventario.
/// Nota: a diferencia de los puertos en Common/Interfaces (que representan servicios
/// externos implementados en Infrastructure), esta interfaz NO es requerida por Clean
/// Architecture: InventoryService se define e implementa en esta misma capa (Application).
/// Se agrega igual por consistencia con el resto del proyecto y para poder mockear
/// el caso de uso en tests de InventoryController.
/// </summary>
public interface IInventoryService
{
    Task<InventoryResponse> GetDotaInventoryAsync(string steamId64, bool force = false, CancellationToken ct = default);
}
