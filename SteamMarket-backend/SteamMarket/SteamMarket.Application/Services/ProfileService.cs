using SteamMarket.Application.Common.Interfaces;
using SteamMarket.Application.DTOs;
using SteamMarket.Application.Services.Interfaces;

namespace SteamMarket.Application.Services;

/// <summary>
/// Caso de uso: leer y actualizar el perfil del usuario (Trade URL + datos personales opcionales;
/// nombre de Steam y SteamID vienen directo de la sesion, no se guardan aparte).
/// </summary>
public sealed class ProfileService : IProfileService
{
    private readonly IUserProfileStore _store;

    public ProfileService(IUserProfileStore store) => _store = store;

    public async Task<ProfileResponse> GetProfileAsync(string steamId64, string name, CancellationToken ct = default)
    {
        var stored = await _store.GetAsync(steamId64, ct);
        return new ProfileResponse
        {
            SteamId = steamId64,
            Name = name,
            TradeUrl = stored?.TradeUrl,
            FirstName = stored?.FirstName,
            LastName = stored?.LastName,
            Email = stored?.Email,
            Phone = stored?.Phone,
            DocumentType = stored?.DocumentType,
            DocumentNumber = stored?.DocumentNumber
        };
    }

    public Task UpdateTradeUrlAsync(string steamId64, string tradeUrl, CancellationToken ct = default) =>
        _store.SetTradeUrlAsync(steamId64, tradeUrl, ct);

    public Task SavePersonalDataAsync(string steamId64, PersonalDataInput data, CancellationToken ct = default) =>
        _store.SavePersonalDataAsync(steamId64, data, ct);

    public Task DeletePersonalDataAsync(string steamId64, CancellationToken ct = default) =>
        _store.ClearPersonalDataAsync(steamId64, ct);
}
