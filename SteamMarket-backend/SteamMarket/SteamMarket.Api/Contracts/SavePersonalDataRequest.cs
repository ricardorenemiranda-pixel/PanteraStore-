namespace SteamMarket.Api.Contracts;

/// <summary>Body de PUT /api/profile/personal-data. Todos los campos son opcionales (el usuario puede llenar solo algunos).</summary>
public sealed class SavePersonalDataRequest
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }

    /// <summary>"DNI", "CE" o "Pasaporte".</summary>
    public string? DocumentType { get; init; }
    public string? DocumentNumber { get; init; }
}
