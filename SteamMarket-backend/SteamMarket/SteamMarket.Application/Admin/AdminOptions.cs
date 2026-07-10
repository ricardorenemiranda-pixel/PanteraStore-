namespace SteamMarket.Application.Admin;

/// <summary>
/// Configuracion del administrador/almacen. Se llena desde appsettings.json (seccion "Admin")
/// o (mejor, para el SteamId64) desde user-secrets/variables de entorno.
///
/// SteamId64 vacio = nadie tiene acceso admin (default seguro): los endpoints /api/admin/*
/// rechazan todo hasta que se configure explicitamente.
/// </summary>
public sealed class AdminOptions
{
    /// <summary>SteamID64 de la cuenta que puede aprobar ordenes/retiros (el dueno del sitio).</summary>
    public string SteamId64 { get; set; } = string.Empty;

    /// <summary>Trade URL de la cuenta "almacen" a la que los usuarios mandan sus items al vender.</summary>
    public string TradeUrl { get; set; } = string.Empty;
}
