using System.ComponentModel.DataAnnotations;
using SteamMarket.Application.Pricing;
using Xunit;

namespace SteamMarket.Tests.Application;

/// <summary>
/// Prueba los [Range] de PricingOptions con la misma API (Validator.TryValidateObject)
/// que usa DependencyInjection.AddApplication() para fallar rapido al arrancar si
/// appsettings.json trae un Margin fuera de rango.
/// </summary>
public class PricingOptionsValidationTests
{
    private static bool IsValid(PricingOptions options) =>
        Validator.TryValidateObject(options, new ValidationContext(options), new List<ValidationResult>(), validateAllProperties: true);

    [Fact]
    public void DefaultOptions_AreValid()
    {
        Assert.True(IsValid(new PricingOptions()));
    }

    [Fact]
    public void Margin_AboveOne_IsInvalid()
    {
        Assert.False(IsValid(new PricingOptions { Margin = 2.5m }));
    }

    [Fact]
    public void Margin_Negative_IsInvalid()
    {
        Assert.False(IsValid(new PricingOptions { Margin = -0.1m }));
    }

    [Fact]
    public void CacheHours_Zero_IsInvalid()
    {
        Assert.False(IsValid(new PricingOptions { CacheHours = 0 }));
    }

    [Fact]
    public void CurrencyId_Zero_IsInvalid()
    {
        Assert.False(IsValid(new PricingOptions { CurrencyId = 0 }));
    }
}
