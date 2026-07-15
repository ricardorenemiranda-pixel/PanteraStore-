using SteamMarket.Application.Common.Interfaces;
using SteamMarket.Application.DTOs;
using SteamMarket.Application.Pricing;
using SteamMarket.Application.Services.Interfaces;

namespace SteamMarket.Application.Services;

/// <summary>
/// Caso de uso de cajas (loot boxes), igual en logica e interfaz a giordota.com/cajas.
///
/// El punto clave del diseno (elegido explicitamente por el dueno del negocio): cuando alguien
/// gana un item, tiene que existir DE VERDAD en el inventario de Steam de la cuenta bot -- no se
/// "inventa" un premio que despues no se puede entregar. Por eso OpenBoxAsync:
///   1) Trae el inventario REAL y actual del bot (mismo ISteamInventoryClient que se usa para
///      leer el inventario de cualquier usuario, pero con el SteamId del bot).
///   2) Excluye los AssetId que ya estan comprometidos con otro premio activo (Reserved o
///      PendingRedeem) -- un mismo item fisico no puede tocarle a dos personas.
///   3) Sortea (por peso) SOLO entre los items del pool que todavia tengan stock disponible.
///      Si ninguno tiene stock, la caja no se puede abrir (y no se cobra nada): es una senal
///      operativa de que el admin tiene que reponer inventario en la cuenta bot, no un bug.
///   4) Recien ahi cobra (o consume el cupo gratis de fidelidad) y reserva ese AssetId puntual
///      para el usuario (LootBoxWin), con un indice unico de por medio para blindar contra dos
///      aperturas simultaneas sorteando el mismo asset.
///
/// El usuario despues decide que hacer con su premio (LootBoxWinDto.Status = "Reserved"):
///   - SellWinAsync: se le acredita el valor de mercado actual a la billetera (mismo
///     IMarketPriceProvider que cotiza el resto del sitio). El item queda en el inventario del
///     bot como stock (podria volver a tocarle a otro usuario en el futuro).
///   - RedeemWinAsync: el bot le manda ese asset puntual por trade (SendGiftTradeOfferAsync).
///     Si el usuario lo acepta en Steam, TradeOfferPollingService llama a CompleteRedeemAsync.
/// </summary>
public sealed class LootBoxService : ILootBoxService
{
    private const int PityThreshold = LootBoxDetailDto.PityThreshold;
    private static readonly Random Rng = new();

    private readonly ILootBoxStore _store;
    private readonly ISteamInventoryClient _inventoryClient;
    private readonly ISteamBotService _bot;
    private readonly IWalletStore _wallet;
    private readonly IMarketPriceProvider _priceProvider;
    private readonly PricingOptions _pricing;
    private readonly IUserProfileStore _profiles;
    private readonly ITradeOfferStore _tradeOffers;

    public LootBoxService(
        ILootBoxStore store,
        ISteamInventoryClient inventoryClient,
        ISteamBotService bot,
        IWalletStore wallet,
        IMarketPriceProvider priceProvider,
        PricingOptions pricing,
        IUserProfileStore profiles,
        ITradeOfferStore tradeOffers)
    {
        _store = store;
        _inventoryClient = inventoryClient;
        _bot = bot;
        _wallet = wallet;
        _priceProvider = priceProvider;
        _pricing = pricing;
        _profiles = profiles;
        _tradeOffers = tradeOffers;
    }

    public async Task<IReadOnlyList<LootBoxDto>> GetBoxesAsync(CancellationToken ct = default)
    {
        var boxes = await _store.GetActiveBoxesAsync(ct);
        return boxes.OrderBy(b => b.Category).ThenBy(b => b.SortOrder).Select(ToDto).ToList();
    }

    public async Task<LootBoxDetailDto?> GetBoxDetailAsync(string slug, string? steamId64, CancellationToken ct = default)
    {
        var box = await _store.GetBySlugAsync(slug, ct);
        if (box is null) return null;

        var pool = await _store.GetPoolItemsAsync(box.Id, ct);
        var pityCount = string.IsNullOrEmpty(steamId64)
            ? 0
            : await _store.GetPurchaseCountAsync(steamId64, box.Id, ct);

        return new LootBoxDetailDto
        {
            Box = ToDto(box),
            Contents = pool.Select(ToDto).ToList(),
            PityCount = pityCount,
        };
    }

    public async Task<LootBoxDemoResult> DemoOpenAsync(string slug, CancellationToken ct = default)
    {
        var box = await _store.GetBySlugAsync(slug, ct);
        if (box is null) return LootBoxDemoResult.Fail("Esa caja no existe.");

        var pool = await _store.GetPoolItemsAsync(box.Id, ct);
        if (pool.Count == 0) return LootBoxDemoResult.Fail("Esta caja todavia no tiene contenido configurado.");

        var picked = WeightedPick(pool, p => p.Weight);
        return LootBoxDemoResult.Ok(ToDto(picked));
    }

    public async Task<LootBoxOpenResult> OpenBoxAsync(string steamId64, string slug, CancellationToken ct = default)
    {
        var box = await _store.GetBySlugAsync(slug, ct);
        if (box is null || !box.IsActive) return LootBoxOpenResult.Fail("Esa caja no existe.");

        var pool = await _store.GetPoolItemsAsync(box.Id, ct);
        if (pool.Count == 0) return LootBoxOpenResult.Fail("Esta caja todavia no tiene contenido configurado.");

        if (!_bot.IsReady || string.IsNullOrWhiteSpace(_bot.BotSteamId64))
            return LootBoxOpenResult.Fail("El sistema de entrega no esta disponible ahora mismo. Intenta mas tarde.");

        // Stock real: inventario actual del bot, agrupado por MarketHashName -> AssetIds libres.
        var botInventory = await _inventoryClient.GetDotaInventoryAsync(_bot.BotSteamId64!, force: true, ct);
        if (!botInventory.Success)
            return LootBoxOpenResult.Fail("No se pudo verificar el stock disponible ahora mismo. Intenta mas tarde.");

        var claimed = (await _store.GetActivelyClaimedBotAssetIdsAsync(ct)).ToHashSet();

        var availableByName = botInventory.Items
            .Where(i => i.Marketable && !claimed.Contains(i.AssetId))
            .GroupBy(i => i.MarketHashName)
            .ToDictionary(g => g.Key, g => g.Select(i => i.AssetId).ToList());

        var inStockPool = pool
            .Where(p => availableByName.TryGetValue(p.MarketHashName, out var ids) && ids.Count > 0)
            .ToList();

        if (inStockPool.Count == 0)
            return LootBoxOpenResult.Fail("Sin stock disponible en esta caja ahora mismo. Intenta mas tarde.");

        // Hasta 3 intentos: si dos aperturas concurrentes chocan por el mismo AssetId (indice
        // unico en LootBoxWin.BotAssetId), se saca ese item de la bolsa y se vuelve a sortear.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (inStockPool.Count == 0)
                return LootBoxOpenResult.Fail("Sin stock disponible en esta caja ahora mismo. Intenta mas tarde.");

            var picked = WeightedPick(inStockPool, p => p.Weight);
            var candidateAssetIds = availableByName[picked.MarketHashName];
            var assetId = candidateAssetIds[Rng.Next(candidateAssetIds.Count)];

            var pityCount = await _store.GetPurchaseCountAsync(steamId64, box.Id, ct);
            var isFree = pityCount >= PityThreshold;

            if (!isFree)
            {
                var debited = await _wallet.TryDebitAsync(steamId64, box.Price, "LootBoxOpen", box.Id.ToString(),
                    $"Caja: {box.Name}", ct);
                if (!debited) return LootBoxOpenResult.Fail("No tienes saldo suficiente para abrir esta caja.");
            }

            var win = await _store.TryCreateWinAsync(box.Id, picked.Id, steamId64, assetId, ct);
            if (win is null)
            {
                // Choco contra otra apertura concurrente: si se pago, se devuelve el saldo y se
                // reintenta con ese asset descartado del pool disponible.
                if (!isFree)
                {
                    await _wallet.CreditAsync(steamId64, box.Price, "Adjustment", box.Id.ToString(),
                        $"Reembolso: item de '{box.Name}' ya fue reclamado por otro usuario", ct);
                }

                candidateAssetIds.Remove(assetId);
                if (candidateAssetIds.Count == 0) inStockPool.Remove(picked);
                continue;
            }

            if (isFree)
                await _store.ResetPurchaseCountAsync(steamId64, box.Id, ct);
            else
                await _store.IncrementPurchaseCountAsync(steamId64, box.Id, ct);

            var newPityCount = isFree ? 0 : Math.Min(pityCount + 1, PityThreshold);
            var dto = ToWinDto(win, box, picked);
            return LootBoxOpenResult.Ok(dto, isFree, newPityCount);
        }

        return LootBoxOpenResult.Fail("No se pudo reservar el premio, intenta de nuevo.");
    }

    public async Task<IReadOnlyList<LootBoxWinDto>> GetMyWinsAsync(string steamId64, CancellationToken ct = default)
    {
        var wins = await _store.GetWinsByUserAsync(steamId64, ct);
        var result = new List<LootBoxWinDto>(wins.Count);

        foreach (var win in wins.OrderByDescending(w => w.WonAtUtc))
        {
            var box = await _store.GetByIdAsync(win.LootBoxId, ct);
            var poolItem = (await _store.GetPoolItemsAsync(win.LootBoxId, ct)).FirstOrDefault(p => p.Id == win.PoolItemId);
            if (box is null || poolItem is null) continue;

            result.Add(ToWinDto(win, box, poolItem));
        }

        return result;
    }

    public async Task<SellWinResult> SellWinAsync(string steamId64, Guid winId, CancellationToken ct = default)
    {
        var win = await _store.GetWinByIdAsync(winId, ct);
        if (win is null || win.SteamId64 != steamId64) return SellWinResult.Fail("Ese premio no existe.");
        if (win.Status != "Reserved") return SellWinResult.Fail("Ese premio ya fue resuelto (vendido, canjeado o en camino).");

        var pool = await _store.GetPoolItemsAsync(win.LootBoxId, ct);
        var poolItem = pool.FirstOrDefault(p => p.Id == win.PoolItemId);
        if (poolItem is null) return SellWinResult.Fail("No se pudo identificar el item ganado.");

        var prices = await _priceProvider.GetPricesAsync(new[] { poolItem.MarketHashName }, force: false, maxLiveFetches: 1, ct);
        if (!prices.TryGetValue(poolItem.MarketHashName, out var quote) || quote is not { Success: true, Price: not null })
            return SellWinResult.Fail("No se pudo cotizar el item ahora mismo, intenta mas tarde.");

        // Ojo: aca se paga el precio de mercado COMPLETO (sin el margen que se aplica cuando un
        // usuario vende SU PROPIO item, ver SellOrderService/PricingOptions.Margin): este no es
        // una compra al usuario, es la conversion a efectivo de un premio que el sitio ya se
        // comprometio a entregarle.
        var amount = quote.Price!.Value;

        await _wallet.CreditAsync(steamId64, amount, "LootBoxSale", win.Id.ToString(),
            $"Venta de premio: {poolItem.DisplayName}", ct);
        await _store.UpdateWinStatusAsync(win.Id, "Sold", DateTime.UtcNow, ct);

        return SellWinResult.Ok(amount);
    }

    public async Task<RedeemWinResult> RedeemWinAsync(string steamId64, Guid winId, CancellationToken ct = default)
    {
        var win = await _store.GetWinByIdAsync(winId, ct);
        if (win is null || win.SteamId64 != steamId64) return RedeemWinResult.Fail("Ese premio no existe.");
        if (win.Status != "Reserved") return RedeemWinResult.Fail("Ese premio ya fue resuelto (vendido, canjeado o en camino).");

        if (!_bot.IsReady) return RedeemWinResult.Fail("El bot no esta conectado ahora mismo. Intenta mas tarde.");

        var profile = await _profiles.GetAsync(steamId64, ct);
        if (string.IsNullOrWhiteSpace(profile?.TradeUrl))
            return RedeemWinResult.Fail("Guarda tu Trade URL en tu perfil antes de canjear un premio.");

        var sendResult = await _bot.SendGiftTradeOfferAsync(steamId64, profile.TradeUrl!, new[] { win.BotAssetId }, ct);
        if (!sendResult.Success || sendResult.TradeOfferId is null)
            return RedeemWinResult.Fail(sendResult.Error ?? "No se pudo enviar el canje. Intenta mas tarde.");

        await _tradeOffers.CreateForLootBoxRedeemAsync(win.Id, sendResult.TradeOfferId.Value, steamId64, ct);
        await _store.UpdateWinStatusAsync(win.Id, "PendingRedeem", null, ct);

        return RedeemWinResult.Ok();
    }

    public async Task CompleteRedeemAsync(Guid winId, CancellationToken ct = default)
    {
        var win = await _store.GetWinByIdAsync(winId, ct);
        if (win is null || win.Status != "PendingRedeem") return;

        await _store.UpdateWinStatusAsync(winId, "Redeemed", DateTime.UtcNow, ct);
    }

    public async Task FailRedeemAsync(Guid winId, string reason, CancellationToken ct = default)
    {
        var win = await _store.GetWinByIdAsync(winId, ct);
        if (win is null || win.Status != "PendingRedeem") return;

        // Vuelve a "Reserved": el usuario sigue siendo el dueno del premio y puede reintentar el
        // canje o venderlo. reason queda en el log del polling, no hay campo para guardarlo aca.
        await _store.UpdateWinStatusAsync(winId, "Reserved", null, ct);
    }

    // --- Administracion del catalogo ---

    public async Task<IReadOnlyList<LootBoxDto>> GetAllBoxesForAdminAsync(CancellationToken ct = default)
    {
        var boxes = await _store.GetAllBoxesAsync(ct);
        return boxes.OrderBy(b => b.Category).ThenBy(b => b.SortOrder).Select(ToDto).ToList();
    }

    public async Task<string?> CreateOrUpdateBoxAsync(LootBoxAdminInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Slug)) return "Falta el slug de la caja.";
        if (string.IsNullOrWhiteSpace(input.Name)) return "Falta el nombre de la caja.";
        if (string.IsNullOrWhiteSpace(input.Category)) return "Falta la categoria de la caja.";
        if (input.Price < 0) return "El precio no puede ser negativo.";
        if (input.MaxItemPrice is < 0) return "El limite de valor no puede ser negativo.";

        await _store.UpsertBoxAsync(input.Slug.Trim(), input.Name.Trim(), input.Category.Trim(),
            input.Price, input.MaxItemPrice, input.ImageUrl, input.SortOrder, input.IsActive, ct);

        return null;
    }

    public async Task<string?> AddPoolItemAsync(string slug, LootBoxPoolItemInput input, CancellationToken ct = default)
    {
        var box = await _store.GetBySlugAsync(slug, ct);
        if (box is null) return "Esa caja no existe.";

        if (string.IsNullOrWhiteSpace(input.MarketHashName)) return "Falta el market_hash_name del item.";
        if (string.IsNullOrWhiteSpace(input.DisplayName)) return "Falta el nombre a mostrar del item.";
        if (input.Weight <= 0) return "El peso debe ser mayor a 0.";

        await _store.AddPoolItemAsync(box.Id, input.MarketHashName.Trim(), input.DisplayName.Trim(),
            input.Hero, input.Slot, input.Type, input.Rarity, input.ImageUrl, input.Weight, ct);

        return null;
    }

    public async Task<string?> RemovePoolItemAsync(Guid poolItemId, CancellationToken ct = default)
    {
        var item = await _store.GetPoolItemByIdAsync(poolItemId, ct);
        if (item is null) return "Ese item del pool no existe.";

        await _store.RemovePoolItemAsync(poolItemId, ct);
        return null;
    }

    public async Task<IReadOnlyList<WarehouseItemDto>> GetWarehouseAsync(string steamId64, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(steamId64))
            return Array.Empty<WarehouseItemDto>();

        var inventory = await _inventoryClient.GetDotaInventoryAsync(steamId64, force: false, ct);
        if (!inventory.Success) return Array.Empty<WarehouseItemDto>();

        var groups = inventory.Items.Where(i => i.Marketable).GroupBy(i => i.MarketHashName).ToList();
        if (groups.Count == 0) return Array.Empty<WarehouseItemDto>();

        var names = groups.Select(g => g.Key).ToList();
        var prices = await _priceProvider.GetPricesAsync(names, force: false, maxLiveFetches: names.Count, ct);

        var result = new List<WarehouseItemDto>(groups.Count);
        foreach (var g in groups)
        {
            var first = g.First();
            prices.TryGetValue(g.Key, out var quote);

            result.Add(new WarehouseItemDto
            {
                MarketHashName = g.Key,
                DisplayName = first.Name,
                Hero = first.Hero,
                Type = first.Type,
                Rarity = first.Quality == "Arcana" ? "Arcana" : (first.Rarity ?? ""),
                ImageUrl = first.IconUrl,
                Quantity = g.Count(),
                Price = quote is { Success: true, Price: not null } ? quote.Price : null,
            });
        }

        return result.OrderBy(r => r.DisplayName).ToList();
    }

    public async Task<string?> AddPoolItemFromStockAsync(string slug, string steamId64, string marketHashName, CancellationToken ct = default)
    {
        var box = await _store.GetBySlugAsync(slug, ct);
        if (box is null) return "Esa caja no existe.";

        if (string.IsNullOrWhiteSpace(steamId64))
            return "No se pudo identificar tu cuenta de Steam.";

        var inventory = await _inventoryClient.GetDotaInventoryAsync(steamId64, force: false, ct);
        if (!inventory.Success) return "No se pudo leer tu inventario ahora mismo (revisa que sea publico).";

        var match = inventory.Items.FirstOrDefault(i => i.Marketable && i.MarketHashName == marketHashName);
        if (match is null) return "Ese item ya no esta disponible en el almacen.";

        var prices = await _priceProvider.GetPricesAsync(new[] { marketHashName }, force: false, maxLiveFetches: 1, ct);
        if (!prices.TryGetValue(marketHashName, out var quote) || quote is not { Success: true, Price: not null })
            return "No se pudo cotizar ese item ahora mismo, intenta mas tarde.";

        var price = quote.Price!.Value;

        if (box.MaxItemPrice is { } cap && price > cap)
            return $"Ese item vale S/ {price:0.00} y supera el limite de esta caja (S/ {cap:0.00}).";

        var rarity = match.Quality == "Arcana" ? "Arcana" : (match.Rarity ?? "");
        var weight = ComputeInverseWeight(price);

        await _store.AddPoolItemAsync(box.Id, marketHashName, match.Name, match.Hero, slot: null,
            type: match.Type, rarity: rarity, imageUrl: match.IconUrl, weight, ct);

        return null;
    }

    // --- Helpers ---

    /// <summary>
    /// Peso (probabilidad relativa) inversamente proporcional al precio: los items baratos
    /// deben salir mucho mas seguido que los caros, pero ningun item queda en 0 (probabilidad
    /// imposible) -- siempre hay una chance minima, por baja que sea. 1000 es una constante fija
    /// elegida para que el rango tipico de precios (centavos hasta cientos de soles) produzca
    /// pesos bien diferenciados entre si sin necesitar ajustar nada por caja.
    /// </summary>
    private static int ComputeInverseWeight(decimal price)
    {
        var safePrice = Math.Max(price, 0.01m);
        return (int)Math.Max(1, Math.Round(1000m / safePrice));
    }

    private static T WeightedPick<T>(IReadOnlyList<T> items, Func<T, int> weightSelector)
    {
        var totalWeight = items.Sum(weightSelector);
        if (totalWeight <= 0) return items[Rng.Next(items.Count)];

        var roll = Rng.Next(totalWeight);
        var cumulative = 0;
        foreach (var item in items)
        {
            cumulative += weightSelector(item);
            if (roll < cumulative) return item;
        }

        return items[^1];
    }

    private static LootBoxWinDto ToWinDto(LootBoxWinRecord win, LootBoxRecord box, LootBoxPoolItemRecord poolItem)
    {
        return new LootBoxWinDto
        {
            Id = win.Id,
            BoxName = box.Name,
            ItemName = poolItem.DisplayName,
            ItemImageUrl = poolItem.ImageUrl,
            Rarity = poolItem.Rarity,
            Status = win.Status,
            WonAtUtc = win.WonAtUtc,
            ResolvedAtUtc = win.ResolvedAtUtc,
        };
    }

    private static LootBoxDto ToDto(LootBoxRecord b) => new()
    {
        Id = b.Id,
        Slug = b.Slug,
        Name = b.Name,
        Category = b.Category,
        Price = b.Price,
        MaxItemPrice = b.MaxItemPrice,
        ImageUrl = b.ImageUrl,
        SortOrder = b.SortOrder,
        IsActive = b.IsActive,
    };

    private static LootBoxPoolItemDto ToDto(LootBoxPoolItemRecord p) => new()
    {
        Id = p.Id,
        MarketHashName = p.MarketHashName,
        DisplayName = p.DisplayName,
        Hero = p.Hero,
        Slot = p.Slot,
        Type = p.Type,
        Rarity = p.Rarity,
        ImageUrl = p.ImageUrl,
        Weight = p.Weight,
    };
}
