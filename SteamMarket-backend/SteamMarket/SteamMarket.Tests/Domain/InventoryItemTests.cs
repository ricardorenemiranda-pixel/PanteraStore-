using SteamMarket.Domain.Entities;
using Xunit;

namespace SteamMarket.Tests.Domain;

/// <summary>
/// InventoryItem.ApplyPricing() es la regla de negocio central del proyecto (cuanto se le
/// paga al usuario por su item). Es dominio puro, sin dependencias: no necesita mocks.
/// </summary>
public class InventoryItemTests
{
    private static InventoryItem MakeItem() => new()
    {
        AssetId = "1",
        MarketHashName = "Inscribed Baby Roshan"
    };

    [Fact]
    public void ApplyPricing_CalculatesPayoutAsMarketPriceTimesMargin()
    {
        var item = MakeItem();

        item.ApplyPricing(100.00m, 0.70m);

        Assert.Equal(100.00m, item.MarketPrice);
        Assert.Equal(70.00m, item.PayoutPrice);
    }

    [Fact]
    public void ApplyPricing_RoundsPayoutToTwoDecimals()
    {
        var item = MakeItem();

        item.ApplyPricing(9.995m, 0.5m); // 9.995 * 0.5 = 4.9975 -> redondea a 5.00

        Assert.Equal(5.00m, item.PayoutPrice);
    }

    [Fact]
    public void ApplyPricing_AllowsFullMarginOfOne()
    {
        var item = MakeItem();

        item.ApplyPricing(10.00m, 1m);

        Assert.Equal(10.00m, item.PayoutPrice);
    }

    [Fact]
    public void ApplyPricing_AllowsZeroMargin()
    {
        var item = MakeItem();

        item.ApplyPricing(10.00m, 0m);

        Assert.Equal(0.00m, item.PayoutPrice);
    }

    [Fact]
    public void ApplyPricing_ThrowsWhenMarketPriceIsNegative()
    {
        var item = MakeItem();

        Assert.Throws<ArgumentOutOfRangeException>(() => item.ApplyPricing(-1m, 0.70m));
    }

    [Fact]
    public void ApplyPricing_ThrowsWhenMarginIsBelowZero()
    {
        var item = MakeItem();

        Assert.Throws<ArgumentOutOfRangeException>(() => item.ApplyPricing(10m, -0.01m));
    }

    [Fact]
    public void ApplyPricing_ThrowsWhenMarginIsAboveOne()
    {
        var item = MakeItem();

        Assert.Throws<ArgumentOutOfRangeException>(() => item.ApplyPricing(10m, 1.01m));
    }
}
