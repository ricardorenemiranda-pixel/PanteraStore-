using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SteamMarket.Application.Admin;
using SteamMarket.Application.Bot;
using SteamMarket.Application.Inventory;
using SteamMarket.Application.Pricing;
using SteamMarket.Application.Services;
using SteamMarket.Application.Services.Interfaces;

namespace SteamMarket.Application;

/// <summary>
/// Registra los servicios de la capa Application.
/// La API llama a services.AddApplication(configuration) en Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ISellOrderService, SellOrderService>();
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IAdminAccountService, AdminAccountService>();
        services.AddScoped<ILootBoxService, LootBoxService>();

        // PricingOptions se llena desde appsettings.json ("Pricing") y se comparte como singleton:
        // tanto InventoryService (Application) como SteamMarketPriceProvider (Infrastructure) la usan.
        // Se valida ACA, al arrancar: si alguien pone Margin: 2.5 en appsettings, el proceso
        // no levanta en vez de pagarle de mas a los usuarios en silencio.
        var pricingOptions = configuration.GetSection("Pricing").Get<PricingOptions>() ?? new PricingOptions();
        ValidateOrThrow(pricingOptions);
        services.AddSingleton(pricingOptions);

        // InventoryCacheOptions ("Inventory" en appsettings): cuanto dura fresco el inventario
        // cacheado en SQLite antes de volver a pedirlo a Steam.
        var inventoryOptions = configuration.GetSection("Inventory").Get<InventoryCacheOptions>() ?? new InventoryCacheOptions();
        ValidateOrThrow(inventoryOptions);
        services.AddSingleton(inventoryOptions);

        // AdminOptions ("Admin" en appsettings/user-secrets): quien puede aprobar ordenes/retiros
        // y a que Trade URL le mandan los items los usuarios al vender. Sin validar a la fuerza:
        // si SteamId64 esta vacio, los endpoints admin simplemente rechazan a todos (default seguro).
        var adminOptions = configuration.GetSection("Admin").Get<AdminOptions>() ?? new AdminOptions();
        services.AddSingleton(adminOptions);

        // BotOptions ("Bot" en user-secrets, NUNCA en appsettings.json: son credenciales reales
        // de una cuenta de Steam). Igual que AdminOptions, sin validar a la fuerza: si esta vacio
        // el bot simplemente no arranca y el sitio sigue con el flujo manual de Trade URL.
        var botOptions = configuration.GetSection("Bot").Get<BotOptions>() ?? new BotOptions();
        services.AddSingleton(botOptions);

        return services;
    }

    private static void ValidateOrThrow<T>(T options) where T : notnull
    {
        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(options, context, results, validateAllProperties: true))
        {
            var errors = string.Join(" | ", results.Select(r => r.ErrorMessage));
            throw new InvalidOperationException(
                $"Configuracion invalida para '{typeof(T).Name}': {errors}");
        }
    }
}
