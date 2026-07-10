using System.ComponentModel.DataAnnotations;

namespace SteamMarket.Application.Pricing;

/// <summary>
/// Configuracion del negocio de precios. Se llena desde appsettings.json (seccion "Pricing").
/// Se valida al arrancar (ver DependencyInjection.AddApplication) para no descubrir un
/// margen invalido (ej. 2.5 = pagar 250%) recien cuando un usuario pide su inventario.
/// </summary>
public sealed class PricingOptions
{
    /// <summary>Que fraccion del precio de mercado se le paga al usuario. 0.70 = 70%.</summary>
    [Range(0.0, 1.0, ErrorMessage = "Margin debe estar entre 0 y 1 (es una fraccion, ej. 0.70 = 70%).")]
    public decimal Margin { get; set; } = 0.70m;

    /// <summary>Cuantas horas se considera "fresco" un precio antes de volver a pedirlo a Steam.</summary>
    [Range(1, 24 * 30, ErrorMessage = "CacheHours debe estar entre 1 hora y 30 dias.")]
    public int CacheHours { get; set; } = 6;

    /// <summary>Codigo de moneda de Steam para priceoverview. 1 = USD.</summary>
    [Range(1, 100, ErrorMessage = "CurrencyId debe ser un codigo de moneda de Steam valido (1 = USD).")]
    public int CurrencyId { get; set; } = 1;

    /// <summary>Monto minimo (en la moneda configurada) para poder confirmar una venta desde el carrito.</summary>
    [Range(0.0, 100000, ErrorMessage = "MinSaleAmount debe ser un monto valido.")]
    public decimal MinSaleAmount { get; set; } = 5m;
}
