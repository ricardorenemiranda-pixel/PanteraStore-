using SteamMarket.Application.DTOs;

namespace SteamMarket.Application.Services.Interfaces;

/// <summary>Contrato del caso de uso de cuentas admin (super admin de config + lista adicional en DB).</summary>
public interface IAdminAccountService
{
    Task<IReadOnlyList<AdminAccountDto>> GetAllAsync(CancellationToken ct = default);

    /// <returns>null si se guardo bien, o un mensaje de error.</returns>
    Task<string?> AddOrUpdateAsync(string steamId64, string tradeUrl, string? label, CancellationToken ct = default);

    /// <returns>null si se quito bien, o un mensaje de error.</returns>
    Task<string?> RemoveAsync(string steamId64, CancellationToken ct = default);

    /// <summary>true si es el super admin de config O esta en la lista adicional.</summary>
    Task<bool> IsAdminAsync(string steamId64, CancellationToken ct = default);

    /// <summary>true solo si es el super admin de config (el unico que puede gestionar la lista).</summary>
    bool IsSuperAdmin(string steamId64);

    /// <summary>Una Trade URL disponible del pool (config primero, si no la primera de la lista).</summary>
    Task<string?> GetAvailableTradeUrlAsync(CancellationToken ct = default);
}
