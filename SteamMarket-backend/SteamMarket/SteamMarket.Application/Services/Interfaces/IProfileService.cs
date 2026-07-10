using SteamMarket.Application.Common.Interfaces;
using SteamMarket.Application.DTOs;

namespace SteamMarket.Application.Services.Interfaces;

/// <summary>Contrato del caso de uso de perfil (ver nota en IInventoryService sobre por que existe esta interfaz).</summary>
public interface IProfileService
{
    Task<ProfileResponse> GetProfileAsync(string steamId64, string name, CancellationToken ct = default);

    // El formato del Trade URL / email / etc ya se valida antes en el controller (FluentValidation);
    // aca solo se orquesta y persiste.
    Task UpdateTradeUrlAsync(string steamId64, string tradeUrl, CancellationToken ct = default);

    Task SavePersonalDataAsync(string steamId64, PersonalDataInput data, CancellationToken ct = default);

    Task DeletePersonalDataAsync(string steamId64, CancellationToken ct = default);
}
