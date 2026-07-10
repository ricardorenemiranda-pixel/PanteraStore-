using SteamMarket.Application.Admin;
using SteamMarket.Application.Common.Interfaces;
using SteamMarket.Application.DTOs;
using SteamMarket.Application.Services.Interfaces;

namespace SteamMarket.Application.Services;

/// <summary>
/// Caso de uso: administrar quien puede aprobar ordenes/retiros. Hay dos niveles:
/// - "Super admin": el SteamId64 de appsettings/user-secrets (Admin:SteamId64). Fijo, no se
///   puede agregar/quitar desde la UI (para eso hay que tocar la config del servidor) y es el
///   unico que puede agregar o quitar OTROS admins.
/// - "Admins": cuentas adicionales guardadas en la tabla AdminAccounts, agregadas por el super
///   admin desde /admin. Pueden aprobar ordenes/retiros, pero no gestionar la lista.
/// </summary>
public sealed class AdminAccountService : IAdminAccountService
{
    private readonly IAdminAccountStore _store;
    private readonly AdminOptions _superAdmin;

    public AdminAccountService(IAdminAccountStore store, AdminOptions superAdmin)
    {
        _store = store;
        _superAdmin = superAdmin;
    }

    public async Task<IReadOnlyList<AdminAccountDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = new List<AdminAccountDto>();

        if (!string.IsNullOrWhiteSpace(_superAdmin.SteamId64))
        {
            list.Add(new AdminAccountDto
            {
                SteamId = _superAdmin.SteamId64,
                TradeUrl = _superAdmin.TradeUrl,
                Label = "Super admin (configuración del servidor)",
                IsSuperAdmin = true,
                AddedAtUtc = null
            });
        }

        var records = await _store.GetAllAsync(ct);
        list.AddRange(records.Select(r => new AdminAccountDto
        {
            SteamId = r.SteamId64,
            TradeUrl = r.TradeUrl,
            Label = r.Label,
            IsSuperAdmin = false,
            AddedAtUtc = r.AddedAtUtc
        }));

        return list;
    }

    public async Task<string?> AddOrUpdateAsync(string steamId64, string tradeUrl, string? label, CancellationToken ct = default)
    {
        if (IsSuperAdmin(steamId64))
            return "Esa cuenta ya es el super admin (viene de la configuración del servidor).";

        await _store.UpsertAsync(steamId64, tradeUrl, label, ct);
        return null;
    }

    public async Task<string?> RemoveAsync(string steamId64, CancellationToken ct = default)
    {
        if (IsSuperAdmin(steamId64))
            return "No se puede quitar al super admin desde acá: hay que cambiar la configuración del servidor.";

        await _store.RemoveAsync(steamId64, ct);
        return null;
    }

    public async Task<bool> IsAdminAsync(string steamId64, CancellationToken ct = default)
    {
        if (IsSuperAdmin(steamId64)) return true;
        return await _store.ExistsAsync(steamId64, ct);
    }

    public bool IsSuperAdmin(string steamId64) =>
        !string.IsNullOrWhiteSpace(_superAdmin.SteamId64) && steamId64 == _superAdmin.SteamId64;

    public async Task<string?> GetAvailableTradeUrlAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_superAdmin.TradeUrl))
            return _superAdmin.TradeUrl;

        var records = await _store.GetAllAsync(ct);
        return records.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.TradeUrl))?.TradeUrl;
    }
}
