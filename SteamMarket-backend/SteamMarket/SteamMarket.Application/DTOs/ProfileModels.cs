namespace SteamMarket.Application.DTOs;

/// <summary>Lo que la API le devuelve al frontend para la pagina de perfil.</summary>
public sealed class ProfileResponse
{
    public string SteamId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? TradeUrl { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? DocumentType { get; init; }
    public string? DocumentNumber { get; init; }
}
