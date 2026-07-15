using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SteamKit2;
using SteamKit2.Authentication;
using SteamMarket.Application.Bot;
using SteamMarket.Application.Common.Interfaces;

namespace SteamMarket.Infrastructure.SteamBot;

/// <summary>
/// Mantiene una sesion persistente de la cuenta bot contra la red de Steam (SteamKit2) y expone
/// las dos operaciones que necesita el resto del sistema: mandar una oferta de intercambio
/// pidiendo items, y consultar en que estado quedo.
///
/// OJO: esta clase usa el endpoint no oficial steamcommunity.com/tradeoffer/new/send para
/// ARMAR y ENVIAR la oferta (Valve no ofrece una Web API publica para esto, ver IEconService:
/// solo tiene GetTradeOffer(s)/GetTradeHistory, de solo lectura). Es el mismo mecanismo que usan
/// todos los sitios de skins; funciona construyendo una "sesion web" (cookies) a partir del
/// login por SteamKit2 en vez de un usuario logueado por navegador.
///
/// Esta es la pieza mas sensible a la version exacta de SteamKit2 instalada (el flujo de login
/// con autenticador cambio varias veces desde 2023). Si "dotnet build" tira errores en esta
/// clase por nombres de metodos/clases que no coinciden con la version resuelta del paquete,
/// son ajustes puntuales sobre esta base, no hay que rehacer el diseno.
/// </summary>
public sealed class SteamBotService : BackgroundService, ISteamBotService
{
    private readonly BotOptions _options;
    private readonly ILogger<SteamBotService> _logger;

    private readonly SteamClient _client;
    private readonly CallbackManager _manager;
    private readonly SteamUser _steamUser;

    private string? _steamId64;
    private string? _accessToken;
    private string? _sessionId;
    private volatile bool _isReady;

    public bool IsReady => _isReady;
    public string? BotSteamId64 => _steamId64;

    public SteamBotService(BotOptions options, ILogger<SteamBotService> logger)
    {
        _options = options;
        _logger = logger;

        _client = new SteamClient();
        _manager = new CallbackManager(_client);
        _steamUser = _client.GetHandler<SteamUser>()!;

        _manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        _manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        _manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning(
                "Bot de Steam no configurado (falta Bot:Username/Password/SharedSecret en " +
                "user-secrets). El sitio sigue funcionando con el flujo manual de Trade URL.");
            return;
        }

        _client.Connect();

        while (!stoppingToken.IsCancellationRequested)
        {
            _manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            await Task.Delay(10, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
        }
    }

    private void OnConnected(SteamClient.ConnectedCallback callback)
    {
        _logger.LogInformation("Bot: conectado a la red de Steam, iniciando sesion...");
        _ = LogOnAsync();
    }

    private async Task LogOnAsync()
    {
        try
        {
            var authSession = await _client.Authentication.BeginAuthSessionViaCredentialsAsync(new AuthSessionDetails
            {
                Username = _options.Username,
                Password = _options.Password,
                IsPersistentSession = true,
                Authenticator = new BotAuthenticator(_options.SharedSecret),
            });

            var pollResponse = await authSession.PollingWaitForResultAsync();

            _accessToken = pollResponse.AccessToken;

            _steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = pollResponse.AccountName,
                AccessToken = pollResponse.RefreshToken,
                ShouldRememberPassword = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bot: fallo el login contra Steam.");
        }
    }

    private void OnDisconnected(SteamClient.DisconnectedCallback callback)
    {
        _isReady = false;

        if (!_options.IsConfigured) return;

        _logger.LogWarning("Bot: se desconecto de Steam, reintentando en 15s...");
        Task.Delay(TimeSpan.FromSeconds(15)).ContinueWith(_ =>
        {
            try { _client.Connect(); }
            catch (Exception ex) { _logger.LogError(ex, "Bot: fallo al reconectar."); }
        });
    }

    private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        if (callback.Result != EResult.OK)
        {
            _logger.LogError("Bot: login rechazado por Steam ({Result}).", callback.Result);
            _isReady = false;
            return;
        }

        _steamId64 = _client.SteamID?.ConvertToUInt64().ToString();
        _sessionId = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        _isReady = _steamId64 is not null && _accessToken is not null;

        _logger.LogInformation("Bot: sesion iniciada como {SteamId}.", _steamId64);
    }

    public Task<SendTradeOfferResult> SendTradeOfferAsync(
        string recipientSteamId64,
        string recipientTradeUrl,
        IReadOnlyList<string> assetIds,
        CancellationToken ct = default) =>
        SendAsync(
            recipientSteamId64,
            recipientTradeUrl,
            meAssetIds: Array.Empty<string>(),
            themAssetIds: assetIds,
            message: "Oferta automatica de SteamMarket por tu venta confirmada.",
            ct);

    public Task<SendTradeOfferResult> SendGiftTradeOfferAsync(
        string recipientSteamId64,
        string recipientTradeUrl,
        IReadOnlyList<string> botAssetIds,
        CancellationToken ct = default) =>
        SendAsync(
            recipientSteamId64,
            recipientTradeUrl,
            meAssetIds: botAssetIds,
            themAssetIds: Array.Empty<string>(),
            message: "Tu premio de una caja de SteamMarket. Acepta el intercambio para recibirlo.",
            ct);

    /// <summary>
    /// Arma y manda la oferta de intercambio. meAssetIds = lo que pone el BOT (lo que le da al
    /// usuario, ej. un premio de caja); themAssetIds = lo que le PIDE al usuario (ej. una venta
    /// confirmada). Los dos casos usan exactamente el mismo endpoint, solo cambia que lado del
    /// JSON lleva los assets.
    /// </summary>
    private async Task<SendTradeOfferResult> SendAsync(
        string recipientSteamId64,
        string recipientTradeUrl,
        IReadOnlyList<string> meAssetIds,
        IReadOnlyList<string> themAssetIds,
        string message,
        CancellationToken ct)
    {
        if (!_isReady || _steamId64 is null || _accessToken is null || _sessionId is null)
            return new SendTradeOfferResult(false, "El bot de Steam no tiene una sesion activa ahora mismo.", null);

        var token = ExtractTradeToken(recipientTradeUrl);
        if (token is null)
            return new SendTradeOfferResult(false, "La Trade URL del usuario no tiene un token valido.", null);

        var payload = new TradeOfferJson
        {
            Me = new TradeOfferSide
            {
                Assets = meAssetIds.Select(id => new TradeOfferAsset { AssetId = id }).ToList(),
            },
            Them = new TradeOfferSide
            {
                Assets = themAssetIds.Select(id => new TradeOfferAsset { AssetId = id }).ToList(),
            },
        };

        var createParams = new TradeOfferCreateParams { TradeOfferAccessToken = token };

        var form = new Dictionary<string, string>
        {
            ["sessionid"] = _sessionId,
            ["serverid"] = "1",
            ["partner"] = recipientSteamId64,
            ["tradeoffermessage"] = message,
            ["json_tradeoffer"] = JsonSerializer.Serialize(payload),
            ["captcha"] = "",
            ["trade_offer_create_params"] = JsonSerializer.Serialize(createParams),
        };

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Cookie",
            $"sessionid={_sessionId}; steamLoginSecure={Uri.EscapeDataString($"{_steamId64}||{_accessToken}")}");
        http.DefaultRequestHeaders.Referrer = new Uri(recipientTradeUrl);

        try
        {
            using var response = await http.PostAsync(
                "https://steamcommunity.com/tradeoffer/new/send",
                new FormUrlEncodedContent(form),
                ct);

            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                return new SendTradeOfferResult(false, $"Steam respondio {(int)response.StatusCode}: {body}", null);

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("tradeofferid", out var idProp) &&
                ulong.TryParse(idProp.GetString(), out var offerId))
            {
                return new SendTradeOfferResult(true, null, offerId);
            }

            return new SendTradeOfferResult(
                false,
                "Steam no devolvio un tradeofferid. Puede que la oferta necesite confirmacion movil manual del bot.",
                null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bot: fallo al enviar la oferta de intercambio.");
            return new SendTradeOfferResult(false, "No se pudo contactar a Steam para enviar la oferta.", null);
        }
    }

    public async Task<BotTradeOfferState?> GetTradeOfferStateAsync(ulong tradeOfferId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey)) return null;

        try
        {
            using var http = new HttpClient();
            var url = "https://api.steampowered.com/IEconService/GetTradeOffer/v1/" +
                       $"?key={_options.ApiKey}&tradeofferid={tradeOfferId}&language=en";

            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
            if (!doc.RootElement.TryGetProperty("response", out var resp) ||
                !resp.TryGetProperty("offer", out var offer) ||
                !offer.TryGetProperty("trade_offer_state", out var stateProp))
            {
                return null;
            }

            return (BotTradeOfferState)stateProp.GetInt32();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bot: fallo al consultar el estado de la oferta {TradeOfferId}.", tradeOfferId);
            return null;
        }
    }

    private static string? ExtractTradeToken(string tradeUrl)
    {
        if (!Uri.TryCreate(tradeUrl, UriKind.Absolute, out var uri)) return null;

        var query = uri.Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0] == "token")
                return Uri.UnescapeDataString(parts[1]);
        }

        return null;
    }
}
