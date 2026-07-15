using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SteamMarket.Application.Bot;
using SteamMarket.Application.Common.Interfaces;
using SteamMarket.Application.Services.Interfaces;

namespace SteamMarket.Infrastructure.SteamBot;

/// <summary>
/// Cada Bot:PollIntervalSeconds revisa el estado de las ofertas de intercambio activas
/// (mandadas por SteamBotService) contra la Web API de Steam. Si el usuario ya la acepto,
/// completa la SellOrder automaticamente (acredita la billetera) sin que un admin tenga que
/// tocar nada; si la rechazo/cancelo/expiro, la marca como rechazada para que quede libre y no
/// se quede "Pending" para siempre.
///
/// Este servicio es un fallback razonable: si el bot no esta configurado o esta desconectado,
/// GetTradeOfferStateAsync no encuentra nada nuevo y las ordenes se quedan "Pending" para que
/// el admin las revise a mano desde /admin, como antes.
/// </summary>
public sealed class TradeOfferPollingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISteamBotService _bot;
    private readonly BotOptions _options;
    private readonly ILogger<TradeOfferPollingService> _logger;

    public TradeOfferPollingService(
        IServiceScopeFactory scopeFactory,
        ISteamBotService bot,
        BotOptions options,
        ILogger<TradeOfferPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _bot = bot;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(10, _options.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TradeOfferPollingService: fallo un ciclo de revision.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var tradeOffers = scope.ServiceProvider.GetRequiredService<ITradeOfferStore>();
        var orders = scope.ServiceProvider.GetRequiredService<ISellOrderService>();
        var lootBoxes = scope.ServiceProvider.GetRequiredService<ILootBoxService>();

        var active = await tradeOffers.GetActiveAsync(ct);
        if (active.Count == 0) return;

        foreach (var offer in active)
        {
            var state = await _bot.GetTradeOfferStateAsync(offer.TradeOfferId, ct);
            if (state is null) continue;

            var isTerminalGood = state == BotTradeOfferState.Accepted;
            var isTerminalBad = state is BotTradeOfferState.Declined or BotTradeOfferState.Expired or
                BotTradeOfferState.Canceled or BotTradeOfferState.InvalidItems or
                BotTradeOfferState.CanceledBySecondFactor;

            if (offer.Kind == "LootBoxRedeem" && offer.LootBoxWinId is { } winId)
            {
                if (isTerminalGood)
                {
                    await tradeOffers.UpdateStatusAsync(offer.Id, (int)state.Value, ct);
                    await lootBoxes.CompleteRedeemAsync(winId, ct);
                    _logger.LogInformation(
                        "TradeOfferPollingService: canje {TradeOfferId} aceptado, premio {WinId} entregado.",
                        offer.TradeOfferId, winId);
                }
                else if (isTerminalBad)
                {
                    await tradeOffers.UpdateStatusAsync(offer.Id, (int)state.Value, ct);
                    await lootBoxes.FailRedeemAsync(winId, $"Oferta de Steam en estado {state}.", ct);
                    _logger.LogWarning(
                        "TradeOfferPollingService: canje {TradeOfferId} termino en {State}, premio {WinId} vuelve a Reserved.",
                        offer.TradeOfferId, state, winId);
                }
                else if (state != BotTradeOfferState.Active && state != BotTradeOfferState.NeedsConfirmation)
                {
                    await tradeOffers.UpdateStatusAsync(offer.Id, (int)state.Value, ct);
                }

                continue;
            }

            if (offer.SellOrderId is not { } sellOrderId) continue;

            if (isTerminalGood)
            {
                await tradeOffers.UpdateStatusAsync(offer.Id, (int)state.Value, ct);
                var completed = await orders.CompleteOrderAsync(sellOrderId, ct);
                _logger.LogInformation(
                    "TradeOfferPollingService: oferta {TradeOfferId} aceptada, orden {SellOrderId} completada={Completed}.",
                    offer.TradeOfferId, sellOrderId, completed);
            }
            else if (isTerminalBad)
            {
                await tradeOffers.UpdateStatusAsync(offer.Id, (int)state.Value, ct);
                await orders.RejectOrderAsync(sellOrderId, $"Oferta de Steam en estado {state}.", ct);
                _logger.LogWarning(
                    "TradeOfferPollingService: oferta {TradeOfferId} termino en {State}, orden {SellOrderId} rechazada.",
                    offer.TradeOfferId, state, sellOrderId);
            }
            else if (state != BotTradeOfferState.Active && state != BotTradeOfferState.NeedsConfirmation)
            {
                // Otros estados (Countered, InEscrow) no cierran la orden solos: quedan visibles
                // para que el admin decida a mano desde /admin.
                await tradeOffers.UpdateStatusAsync(offer.Id, (int)state.Value, ct);
            }
        }
    }
}
