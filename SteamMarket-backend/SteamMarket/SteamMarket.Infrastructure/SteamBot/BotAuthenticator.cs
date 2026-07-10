using SteamKit2.Authentication;

namespace SteamMarket.Infrastructure.SteamBot;

/// <summary>
/// Le dice a SteamKit2 como resolver el codigo de Steam Guard cuando inicia sesion: en vez de
/// pedirselo a un humano (como haria la app oficial), lo calculamos solos a partir del
/// shared_secret guardado en Bot:SharedSecret (ver SteamTotp).
/// </summary>
public sealed class BotAuthenticator : IAuthenticator
{
    private readonly string _sharedSecret;

    public BotAuthenticator(string sharedSecret)
    {
        _sharedSecret = sharedSecret;
    }

    public Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
    {
        return Task.FromResult(SteamTotp.GenerateAuthCode(_sharedSecret));
    }

    public Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
    {
        // La cuenta bot debe tener Steam Guard movil (no por email); si Steam pide codigo por
        // correo es que el autenticador no quedo bien vinculado.
        throw new InvalidOperationException(
            "Steam pidio un codigo por email para la cuenta bot. Verifica que el autenticador " +
            "movil (Bot:SharedSecret) este vinculado correctamente a esa cuenta.");
    }

    public Task<bool> AcceptDeviceConfirmationAsync()
    {
        // No usamos confirmacion por push del celular, solo el codigo TOTP.
        return Task.FromResult(false);
    }
}
