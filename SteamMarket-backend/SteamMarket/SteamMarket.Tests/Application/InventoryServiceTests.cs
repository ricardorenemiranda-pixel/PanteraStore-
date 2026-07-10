using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SteamMarket.Application.Common.Interfaces;
using SteamMarket.Application.Pricing;
using SteamMarket.Application.Services;
using SteamMarket.Domain.Entities;
using Xunit;

namespace SteamMarket.Tests.Application;

/// <summary>
/// InventoryService es el caso de uso que orquesta ISteamInventoryClient + IMarketPriceProvider.
/// Como ambos son puertos (interfaces), se mockean con Moq: estos tests no hablan con
/// Steam ni con SQLite de verdad, solo prueban la logica de orquestacion.
/// </summary>
public class InventoryServiceTests
{
    private static readonly PricingOptions DefaultPricing = new() { Margin = 0.70m, CacheHours = 6, CurrencyId = 1 };

    // rarity = "Immortal" por defecto: desde que InventoryService solo pide precio para items
    // "valiosos" (IsHighValue), los tests necesitan que los items de prueba cuenten como tales,
    // salvo el test que prueba explicitamente lo contrario.
    private static InventoryItem MakeItem(
        string assetId, string marketHashName, bool marketable = true, bool tradable = true, string? rarity = "Immortal") =>
        new()
        {
            AssetId = assetId,
            ClassId = "class-" + assetId,
            Name = marketHashName,
            MarketHashName = marketHashName,
            Type = "Courier",
            Tradable = tradable,
            Marketable = marketable,
            Rarity = rarity
        };

    [Fact]
    public async Task GetDotaInventoryAsync_WhenInventoryFetchFails_ReturnsFailureWithoutCallingPriceProvider()
    {
        var inventoryClient = new Mock<ISteamInventoryClient>();
        inventoryClient
            .Setup(c => c.GetDotaInventoryAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SteamFetchResult.Fail("El inventario es privado."));

        var priceProvider = new Mock<IMarketPriceProvider>();

        var sut = new InventoryService(inventoryClient.Object, priceProvider.Object, DefaultPricing, NullLogger<InventoryService>.Instance);

        var result = await sut.GetDotaInventoryAsync("76561198000000000");

        Assert.False(result.Success);
        Assert.Equal("El inventario es privado.", result.Error);
        Assert.Empty(result.Items);

        priceProvider.Verify(
            p => p.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetDotaInventoryAsync_AppliesPayoutOnlyToMarketableItemsWithSuccessfulQuote()
    {
        var priced = MakeItem("1", "Inscribed Baby Roshan", marketable: true);
        var notMarketable = MakeItem("2", "Regalo intransferible", marketable: false);
        var noQuote = MakeItem("3", "Item sin listings", marketable: true);

        var inventoryClient = new Mock<ISteamInventoryClient>();
        inventoryClient
            .Setup(c => c.GetDotaInventoryAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SteamFetchResult.Ok(new[] { priced, notMarketable, noQuote }));

        var priceProvider = new Mock<IMarketPriceProvider>();
        priceProvider
            .Setup(p => p.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, MarketPriceResult>
            {
                ["Inscribed Baby Roshan"] = new(true, 100.00m, false, null),
                ["Item sin listings"] = new(false, null, false, "Sin listings recientes."),
            });

        var sut = new InventoryService(inventoryClient.Object, priceProvider.Object, DefaultPricing, NullLogger<InventoryService>.Instance);

        var result = await sut.GetDotaInventoryAsync("76561198000000000");

        Assert.True(result.Success);
        Assert.Equal(3, result.Count);

        var pricedDto = Assert.Single(result.Items, i => i.AssetId == "1");
        Assert.Equal(100.00m, pricedDto.MarketPrice);
        Assert.Equal(70.00m, pricedDto.PayoutPrice); // 100 * 0.70

        var notMarketableDto = Assert.Single(result.Items, i => i.AssetId == "2");
        Assert.Null(notMarketableDto.MarketPrice);
        Assert.Null(notMarketableDto.PayoutPrice);

        var noQuoteDto = Assert.Single(result.Items, i => i.AssetId == "3");
        Assert.Null(noQuoteDto.MarketPrice);
        Assert.Null(noQuoteDto.PayoutPrice);
    }

    [Fact]
    public async Task GetDotaInventoryAsync_DeduplicatesMarketHashNamesBeforeRequestingPrices()
    {
        var itemA = MakeItem("1", "Item duplicado");
        var itemB = MakeItem("2", "Item duplicado"); // mismo MarketHashName que itemA

        var inventoryClient = new Mock<ISteamInventoryClient>();
        inventoryClient
            .Setup(c => c.GetDotaInventoryAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SteamFetchResult.Ok(new[] { itemA, itemB }));

        List<string>? requestedNames = null;
        var priceProvider = new Mock<IMarketPriceProvider>();
        priceProvider
            .Setup(p => p.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<string>, bool, int?, CancellationToken>((names, _, _, _) => requestedNames = names.ToList())
            .ReturnsAsync(new Dictionary<string, MarketPriceResult>());

        var sut = new InventoryService(inventoryClient.Object, priceProvider.Object, DefaultPricing, NullLogger<InventoryService>.Instance);

        await sut.GetDotaInventoryAsync("76561198000000000");

        Assert.NotNull(requestedNames);
        Assert.Single(requestedNames!);
    }

    [Fact]
    public async Task GetDotaInventoryAsync_WhenNoItemsAreMarketable_DoesNotCallPriceProvider()
    {
        var item = MakeItem("1", "No marketable", marketable: false);

        var inventoryClient = new Mock<ISteamInventoryClient>();
        inventoryClient
            .Setup(c => c.GetDotaInventoryAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SteamFetchResult.Ok(new[] { item }));

        var priceProvider = new Mock<IMarketPriceProvider>();

        var sut = new InventoryService(inventoryClient.Object, priceProvider.Object, DefaultPricing, NullLogger<InventoryService>.Instance);

        var result = await sut.GetDotaInventoryAsync("76561198000000000");

        Assert.True(result.Success);
        Assert.Single(result.Items);

        priceProvider.Verify(
            p => p.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetDotaInventoryAsync_WhenItemIsMarketableButNotHighValue_DoesNotRequestPrice()
    {
        // Marketable=true pero Rarity=null (ej. un item comun) no deberia gastar cupo de
        // pedidos a Steam: el frontend nunca lo muestra, no tiene sentido cotizarlo.
        var common = MakeItem("1", "Item comun", marketable: true, rarity: null);

        var inventoryClient = new Mock<ISteamInventoryClient>();
        inventoryClient
            .Setup(c => c.GetDotaInventoryAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SteamFetchResult.Ok(new[] { common }));

        var priceProvider = new Mock<IMarketPriceProvider>();

        var sut = new InventoryService(inventoryClient.Object, priceProvider.Object, DefaultPricing, NullLogger<InventoryService>.Instance);

        var result = await sut.GetDotaInventoryAsync("76561198000000000");

        Assert.True(result.Success);
        Assert.Single(result.Items);
        Assert.Null(result.Items[0].PayoutPrice);

        priceProvider.Verify(
            p => p.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
