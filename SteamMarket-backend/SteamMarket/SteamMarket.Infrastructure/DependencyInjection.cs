using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SteamMarket.Application.Common.Interfaces;
using SteamMarket.Infrastructure.Persistence;
using SteamMarket.Infrastructure.Pricing;
using SteamMarket.Infrastructure.Steam;
using SteamMarket.Infrastructure.SteamBot;

namespace SteamMarket.Infrastructure;

/// <summary>
/// Registra los servicios de la capa Infrastructure.
/// Aqui se "conecta" cada interfaz de Application con su implementacion real.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // HttpClient tipado: cada vez que alguien pida ISteamInventoryClient,
        // recibe un SteamInventoryClient con su propio HttpClient.
        // AutomaticDecompression: steamcommunity.com (via su CDN) a veces devuelve
        // respuestas comprimidas (gzip/deflate/br) incluso en paginas de error, y sin esto
        // ReadAsStringAsync() lee los bytes crudos comprimidos como si fueran texto (mojibake).
        services.AddHttpClient<ISteamInventoryClient, SteamInventoryClient>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "SteamMarket/1.0");
            client.Timeout = TimeSpan.FromSeconds(20);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        });

        // Base de datos Postgres (Supabase por ahora, Railway cuando se despliegue -- mismo motor,
        // asi que solo cambia el connection string, no el codigo). NUNCA va en appsettings.json:
        // trae usuario/contrasena reales de la base. Se configura con:
        //   dotnet user-secrets set "ConnectionStrings:Default" "Host=...;Database=...;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true"
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Falta ConnectionStrings:Default. Configuralo con " +
                "'dotnet user-secrets set \"ConnectionStrings:Default\" \"<tu connection string de Supabase>\"' " +
                "antes de correr la API.");
        }

        services.AddDbContext<SteamMarketDbContext>(options => options.UseNpgsql(connectionString));

        // HttpClient tipado para el proveedor de precios (Steam Market priceoverview).
        // Mismo motivo para AutomaticDecompression que arriba.
        services.AddHttpClient<IMarketPriceProvider, SteamMarketPriceProvider>(client =>
        {
            client.BaseAddress = new Uri("https://steamcommunity.com");
            client.DefaultRequestHeaders.Add("User-Agent", "SteamMarket/1.0");
            client.Timeout = TimeSpan.FromSeconds(15);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        });

        // Cotiza precios en segundo plano (no bloquea al usuario esperando la pagina).
        // Ver InventoryPriceWarmupService para el detalle.
        services.AddHostedService<InventoryPriceWarmupService>();

        // Perfil del usuario (Trade URL + datos personales), persistido en la misma SQLite.
        services.AddScoped<IUserProfileStore, EfUserProfileStore>();

        // Billetera interna (saldo, movimientos, retiros) y ordenes de venta ("confirmar venta").
        services.AddScoped<IWalletStore, EfWalletStore>();
        services.AddScoped<ISellOrderStore, EfSellOrderStore>();

        // Cuentas admin adicionales (aparte del super admin de config).
        services.AddScoped<IAdminAccountStore, EfAdminAccountStore>();

        // Bot de Steam: sesion persistente + envio automatico de ofertas de intercambio.
        // Un solo SteamBotService cumple dos roles: BackgroundService (mantiene la sesion viva,
        // se registra como hosted service) y ISteamBotService (lo que usan SellOrderService y el
        // polling para mandar/consultar ofertas) -- por eso se registra como Singleton y se
        // expone bajo las dos interfaces con el mismo objeto.
        services.AddSingleton<SteamBotService>();
        services.AddSingleton<ISteamBotService>(sp => sp.GetRequiredService<SteamBotService>());
        services.AddHostedService(sp => sp.GetRequiredService<SteamBotService>());

        services.AddScoped<ITradeOfferStore, EfTradeOfferStore>();
        services.AddHostedService<TradeOfferPollingService>();

        return services;
    }
}
