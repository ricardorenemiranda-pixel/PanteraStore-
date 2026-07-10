namespace SteamMarket.Domain.ValueObjects;

/// <summary>
/// Representa el identificador de un usuario de Steam (SteamID64).
/// Es un "value object": no tiene identidad propia, solo su valor importa.
/// </summary>
public readonly record struct SteamId(string Value)
{
    /// <summary>
    /// El login de Steam entrega el id como una URL:
    /// https://steamcommunity.com/openid/id/76561198000000000
    /// Este metodo extrae solo el numero.
    /// </summary>
    public static SteamId FromOpenIdUrl(string? openIdUrl)
    {
        if (string.IsNullOrEmpty(openIdUrl))
            return new SteamId(string.Empty);

        var idx = openIdUrl.LastIndexOf('/');
        var id = idx >= 0 ? openIdUrl[(idx + 1)..] : openIdUrl;
        return new SteamId(id);
    }

    public bool IsValid => !string.IsNullOrWhiteSpace(Value) && Value.All(char.IsDigit);

    /// <summary>
    /// Convierte el SteamID64 al "AccountID" de 32 bits que usan los endpoints viejos de Steam
    /// (por ejemplo el campo "partner" al armar una oferta de intercambio, o el numero que
    /// aparece en la Trade URL). Formula fija de Valve: AccountID = SteamID64 - 76561197960265728.
    /// </summary>
    public uint ToAccountId32()
    {
        const ulong SteamId64Base = 76561197960265728UL;
        if (!ulong.TryParse(Value, out var id64) || id64 < SteamId64Base)
            return 0;

        return (uint)(id64 - SteamId64Base);
    }

    public override string ToString() => Value;
}
