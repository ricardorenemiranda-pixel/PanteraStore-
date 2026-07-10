namespace SteamMarket.Application.Common.Interfaces;

/// <summary>Snapshot de lo guardado para un usuario (todo opcional: puede no existir fila todavia).</summary>
public sealed class StoredProfile
{
    public string? TradeUrl { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? DocumentType { get; init; }
    public string? DocumentNumber { get; init; }
}

/// <summary>Datos personales a guardar (ver ClearPersonalDataAsync para "olvidarlos").</summary>
public sealed class PersonalDataInput
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? DocumentType { get; init; }
    public string? DocumentNumber { get; init; }
}

/// <summary>
/// Puerto hacia la persistencia de datos de perfil (Trade URL + datos personales opcionales).
/// Application no sabe si esto vive en SQLite, Postgres (Supabase) o donde sea: eso lo decide
/// Infrastructure.
/// </summary>
public interface IUserProfileStore
{
    Task<StoredProfile?> GetAsync(string steamId64, CancellationToken ct = default);

    Task SetTradeUrlAsync(string steamId64, string tradeUrl, CancellationToken ct = default);

    Task SavePersonalDataAsync(string steamId64, PersonalDataInput data, CancellationToken ct = default);

    /// <summary>Borra (pone en null) los datos personales, pero conserva el Trade URL: ese es
    /// operativo para poder venderle items, no es un "dato personal" en el mismo sentido.</summary>
    Task ClearPersonalDataAsync(string steamId64, CancellationToken ct = default);
}
