using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using SteamMarket.Application.Common.Interfaces;
using SteamMarket.Domain.Entities;
using SteamMarket.Infrastructure.Persistence;

namespace SteamMarket.Infrastructure.Pricing;

/// <summary>
/// Cotiza precios en segundo plano, sin depender de que un usuario este esperando la pagina.
///
/// Por que existe: el endpoint interactivo (InventoryController -> InventoryService) pide
/// precios con un tope chico (para que la pagina cargue rapido siempre), asi que si hay
/// muchos items sin cotizar, se completan de a poco en varias recargas. Este servicio hace
/// ese trabajo solo, cada cierto tiempo, con un tope mas alto, hasta cubrir todo el inventario
/// cacheado sin que el usuario tenga que hacer nada.
///
/// Sigue respetando el throttle global de Steam (1 pedido a la vez, 1.2s de por medio) porque
/// llama al mismo IMarketPriceProvider que usa el endpoint interactivo.
/// </summary>
public sealed class InventoryPriceWarmupService : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private const int MaxLiveFetchesPerTick = 40;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InventoryPriceWarmupService> _logger;

    public InventoryPriceWarmupService(IServiceScopeFactory scopeFactory, ILogger<InventoryPriceWarmupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await WarmupOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo el warmup de precios en segundo plano (se reintenta en el proximo ciclo).");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task WarmupOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SteamMarketDbContext>();
        var priceProvider = scope.ServiceProvider.GetRequiredService<IMarketPriceProvider>();

        var inventories = await db.Inventories.AsNoTracking().ToListAsync(ct);
        if (inventories.Count == 0) return;

        // Juntamos los nombres de items "valiosos" y marketable de TODOS los inventarios
        // cacheados (por ahora, un solo usuario en dev; a futuro, todos los que hayan
        // cargado su inventario alguna vez).
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var inventory in inventories)
        {
            List<InventoryItem>? items;
            try
            {
                items = JsonSerializer.Deserialize<List<InventoryItem>>(inventory.ItemsJson);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "No se pudo leer el inventario cacheado de {SteamId} para el warmup.", inventory.SteamId64);
                continue;
            }

            if (items is null) continue;

            foreach (var item in items)
            {
                if (item.Marketable && item.IsHighValue() && !string.IsNullOrWhiteSpace(item.MarketHashName))
                    names.Add(item.MarketHashName);
            }
        }

        if (names.Count == 0) return;

        _logger.LogInformation("Warmup de precios: revisando {Count} items valiosos (tope {Limit} por ciclo).", names.Count, MaxLiveFetchesPerTick);

        // force=false a proposito: solo pide a Steam lo que realmente falta o esta vencido,
        // igual que el endpoint interactivo. No queremos volver a pedir lo que ya esta fresco.
        await priceProvider.GetPricesAsync(names, force: false, maxLiveFetches: MaxLiveFetchesPerTick, ct);
    }
}
