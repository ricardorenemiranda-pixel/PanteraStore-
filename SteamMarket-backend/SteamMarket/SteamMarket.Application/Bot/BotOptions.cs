namespace SteamMarket.Application.Bot;

/// <summary>
/// Credenciales de la cuenta "bot" de Steam que arma y envia automaticamente las ofertas de
/// intercambio pidiendo los items que un usuario confirmo vender.
///
/// OJO SEGURIDAD: esto es literalmente el usuario/contrasena de una cuenta real de Steam
/// (mas los secretos de Steam Guard movil, que dan control TOTAL de esa cuenta si se filtran:
/// permiten generar codigos 2FA y confirmar cualquier cosa como si fueras el dueno).
/// NUNCA va en appsettings.json. Solo via user-secrets en dev o variables de entorno en
/// produccion:
///   dotnet user-secrets set "Bot:Username" "..."
///   dotnet user-secrets set "Bot:Password" "..."
///   dotnet user-secrets set "Bot:SharedSecret" "..."
///   dotnet user-secrets set "Bot:IdentitySecret" "..."
///   dotnet user-secrets set "Bot:ApiKey" "..."
///
/// Si Username/Password estan vacios, el bot simplemente no arranca (SteamBotService se queda
/// "no listo") y el sitio sigue funcionando con el flujo manual (mostrar la Trade URL del admin).
/// </summary>
public sealed class BotOptions
{
    /// <summary>Usuario de la cuenta bot (almacen).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Contrasena de la cuenta bot.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>shared_secret del autenticador movil de Steam Guard de esa cuenta (para generar
    /// el codigo 2FA al iniciar sesion). Se obtiene solo al vincular el autenticador; si la cuenta
    /// ya tiene Steam Guard movil activado normalmente hace falta revincularlo con una herramienta
    /// tipo SteamDesktopAuthenticator para poder leer este valor.</summary>
    public string SharedSecret { get; set; } = string.Empty;

    /// <summary>identity_secret de la misma cuenta, usado para firmar confirmaciones moviles de
    /// intercambios (algunas ofertas salientes las exige Steam aunque el bot no entregue nada).</summary>
    public string IdentitySecret { get; set; } = string.Empty;

    /// <summary>API key de Steam Web API de esta cuenta (steamcommunity.com/dev/apikey), usada
    /// para consultar el estado de las ofertas (IEconService/GetTradeOffers).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Cada cuanto se revisa el estado de las ofertas pendientes.</summary>
    public int PollIntervalSeconds { get; set; } = 30;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password) &&
        !string.IsNullOrWhiteSpace(SharedSecret);
}
