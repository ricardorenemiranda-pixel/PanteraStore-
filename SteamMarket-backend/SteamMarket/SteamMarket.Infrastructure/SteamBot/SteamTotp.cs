using System.Security.Cryptography;

namespace SteamMarket.Infrastructure.SteamBot;

/// <summary>
/// Genera codigos de Steam Guard (autenticador movil) a partir del shared_secret de la cuenta,
/// igual que hace la app oficial de Steam. Es el algoritmo TOTP estandar (HMAC-SHA1, ventanas de
/// 30 segundos) pero con el alfabeto de 26 caracteres propio de Steam en vez de digitos.
/// Este algoritmo es publico y estable (lo usan por igual SteamAuth, node-steam-totp, etc.),
/// no depende de la version de ninguna libreria.
/// </summary>
public static class SteamTotp
{
    private const string Alphabet = "23456789BCDFGHJKMNPQRTVWXY";

    public static string GenerateAuthCode(string sharedSecret, long? unixTimeSeconds = null)
    {
        if (string.IsNullOrWhiteSpace(sharedSecret))
            throw new InvalidOperationException("Bot:SharedSecret no esta configurado.");

        var time = unixTimeSeconds ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var secretBytes = Convert.FromBase64String(sharedSecret);

        var timeBytes = BitConverter.GetBytes(time / 30L);
        if (BitConverter.IsLittleEndian) Array.Reverse(timeBytes);

        using var hmac = new HMACSHA1(secretBytes);
        var hash = hmac.ComputeHash(timeBytes);

        var offset = hash[^1] & 0x0F;
        var fullCode = (uint)(
            ((hash[offset] & 0x7f) << 24) |
            ((hash[offset + 1] & 0xff) << 16) |
            ((hash[offset + 2] & 0xff) << 8) |
            (hash[offset + 3] & 0xff));

        var code = new char[5];
        for (var i = 0; i < 5; i++)
        {
            code[i] = Alphabet[(int)(fullCode % Alphabet.Length)];
            fullCode /= (uint)Alphabet.Length;
        }

        return new string(code);
    }
}
