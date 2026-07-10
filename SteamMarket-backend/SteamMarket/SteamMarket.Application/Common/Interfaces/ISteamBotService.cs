namespace SteamMarket.Application.Common.Interfaces;

/// <summary>Estados de una oferta de intercambio, tal como los reporta IEconService/GetTradeOffers.
/// Los valores numericos son los que usa Steam de verdad (ETradeOfferState).</summary>
public enum BotTradeOfferState
{
    Invalid = 1,
    Active = 2,
    Accepted = 3,
    Countered = 4,
    Expired = 5,
    Canceled = 6,
    Declined = 7,
    InvalidItems = 8,
    NeedsConfirmation = 9,
    CanceledBySecondFactor = 10,
    InEscrow = 11
}

public sealed record SendTradeOfferResult(bool Success, string? Error, ulong? TradeOfferId);

/// <summary>
/// Puerto hacia la cuenta bot de Steam: arma y manda ofertas de intercambio pidiendo items
/// (sin dar nada a cambio) y consulta su estado. La implementacion real (Infrastructure) usa
/// una sesion persistente contra la red de Steam; ver SteamBotService.
/// </summary>
public interface ISteamBotService
{
    /// <summary>True si el bot logro iniciar sesion y tiene una sesion web utilizable ahora mismo.</summary>
    bool IsReady { get; }

    /// <summary>
    /// Arma y envia una oferta de intercambio desde la cuenta bot hacia recipientSteamId64,
    /// pidiendo exactamente los assetIds indicados (inventario de Dota 2, appid 570 contextid 2).
    /// No requiere que el bot sea "amigo" del usuario: usa el token de la Trade URL.
    /// </summary>
    Task<SendTradeOfferResult> SendTradeOfferAsync(
        string recipientSteamId64,
        string recipientTradeUrl,
        IReadOnlyList<string> assetIds,
        CancellationToken ct = default);

    /// <summary>Consulta el estado actual de una oferta ya enviada. Null si no se pudo consultar.</summary>
    Task<BotTradeOfferState?> GetTradeOfferStateAsync(ulong tradeOfferId, CancellationToken ct = default);
}
