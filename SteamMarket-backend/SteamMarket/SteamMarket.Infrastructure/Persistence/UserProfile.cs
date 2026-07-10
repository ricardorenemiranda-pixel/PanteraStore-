namespace SteamMarket.Infrastructure.Persistence;

/// <summary>
/// Datos de perfil del usuario que no vienen de Steam (no hay tabla de "usuarios" propia:
/// la identidad real la maneja Steam OpenID, esto solo guarda preferencias/datos que el
/// usuario configura en nuestro sitio, como su Trade URL).
/// </summary>
public sealed class UserProfile
{
    public string SteamId64 { get; set; } = string.Empty;

    /// <summary>
    /// URL de intercambio de Steam (Perfil de Steam -> Inventario -> Trade Offers -> "Quien puede
    /// enviarme ofertas de intercambio" -> "Trade URL"). La necesitamos para poder mandarle
    /// ofertas de trade sin ser sus amigos. Null hasta que el usuario la configure.
    /// </summary>
    public string? TradeUrl { get; set; }

    // --- Datos personales (opcionales, el usuario los llena en /perfil) ---
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>"DNI", "CE" (Carne de Extranjeria) o "Pasaporte".</summary>
    public string? DocumentType { get; set; }
    public string? DocumentNumber { get; set; }

    /// <summary>
    /// Saldo interno en soles (billetera del sitio). Se mueve solo via WalletTransactions
    /// (ver EfWalletStore): esta columna es un cache del total para leer rapido, nunca se
    /// edita "a mano" fuera de un movimiento registrado.
    /// </summary>
    public decimal Balance { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
