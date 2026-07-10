namespace SteamMarket.Infrastructure.Persistence;

/// <summary>
/// Una cuenta admin adicional (aparte del "super admin" fijo en appsettings/user-secrets,
/// Admin:SteamId64). Sirve como cuenta almacen alternativa: si una no tiene espacio para
/// recibir el trade de una venta, el sistema puede ofrecer otra.
/// </summary>
public sealed class AdminAccount
{
    public string SteamId64 { get; set; } = string.Empty;
    public string TradeUrl { get; set; } = string.Empty;

    /// <summary>Nombre para reconocerla en el panel (ej. "Almacen 2"). Opcional.</summary>
    public string? Label { get; set; }

    public DateTime AddedAtUtc { get; set; }
}
