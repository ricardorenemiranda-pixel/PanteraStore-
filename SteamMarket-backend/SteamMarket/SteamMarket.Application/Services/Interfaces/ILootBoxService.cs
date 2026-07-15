using SteamMarket.Application.DTOs;

namespace SteamMarket.Application.Services.Interfaces;

/// <summary>Contrato del caso de uso de cajas (loot boxes): listar catalogo, abrir cajas,
/// gestionar premios ganados (vender o canjear) y administrar el catalogo.</summary>
public interface ILootBoxService
{
    Task<IReadOnlyList<LootBoxDto>> GetBoxesAsync(CancellationToken ct = default);

    /// <summary>steamId64 null = usuario no logueado (PityCount siempre 0).</summary>
    Task<LootBoxDetailDto?> GetBoxDetailAsync(string slug, string? steamId64, CancellationToken ct = default);

    /// <summary>"PRUEBA GRATIS": sortea un item de preview del pool completo (sin mirar stock
    /// real), no cobra nada y no genera un premio guardado.</summary>
    Task<LootBoxDemoResult> DemoOpenAsync(string slug, CancellationToken ct = default);

    /// <summary>Abre la caja de verdad: cobra (o consume el cupo gratis por fidelidad), sortea
    /// entre los items del pool que tengan stock real disponible en el bot, y reserva ese item
    /// para el usuario.</summary>
    Task<LootBoxOpenResult> OpenBoxAsync(string steamId64, string slug, CancellationToken ct = default);

    Task<IReadOnlyList<LootBoxWinDto>> GetMyWinsAsync(string steamId64, CancellationToken ct = default);

    /// <summary>Cambia el premio ganado por su valor de mercado actual, acreditado a la billetera.</summary>
    Task<SellWinResult> SellWinAsync(string steamId64, Guid winId, CancellationToken ct = default);

    /// <summary>Pide que el bot le mande el item real por Steam trade (requiere Trade URL guardada).</summary>
    Task<RedeemWinResult> RedeemWinAsync(string steamId64, Guid winId, CancellationToken ct = default);

    /// <summary>Llamado por TradeOfferPollingService cuando el trade de entrega fue aceptado.</summary>
    Task CompleteRedeemAsync(Guid winId, CancellationToken ct = default);

    /// <summary>Llamado por TradeOfferPollingService cuando el trade de entrega fallo/expiro: el
    /// premio vuelve a "Reserved" para que el usuario pueda reintentar.</summary>
    Task FailRedeemAsync(Guid winId, string reason, CancellationToken ct = default);

    // --- Administracion del catalogo (solo admin, ver AdminController) ---

    Task<IReadOnlyList<LootBoxDto>> GetAllBoxesForAdminAsync(CancellationToken ct = default);

    Task<string?> CreateOrUpdateBoxAsync(LootBoxAdminInput input, CancellationToken ct = default);

    Task<string?> AddPoolItemAsync(string slug, LootBoxPoolItemInput input, CancellationToken ct = default);

    Task<string?> RemovePoolItemAsync(Guid poolItemId, CancellationToken ct = default);

    /// <summary>
    /// Inventario REAL y actual de la cuenta de Steam indicada ("almacen"), cotizado, para elegir
    /// que agregar a una caja. Usa el endpoint publico de inventario de Steam (el mismo que
    /// /inventario), NO requiere que el bot este conectado -- sirve para armar/probar el pool de
    /// una caja usando la propia cuenta del admin mientras el bot todavia no esta configurado.
    /// Una vez que el bot este conectado y sea la cuenta que realmente entrega los premios, hay
    /// que pasar su BotSteamId64 aca para que "almacen" refleje stock realmente entregable.
    /// Lista vacia si esa cuenta no tiene inventario publico o no se pudo leer.
    /// </summary>
    Task<IReadOnlyList<WarehouseItemDto>> GetWarehouseAsync(string steamId64, CancellationToken ct = default);

    /// <summary>
    /// Agrega un item al pool de la caja tomando sus datos (nombre, heroe, tipo, rareza, imagen)
    /// directo del inventario real de la cuenta indicada (ver GetWarehouseAsync) -- el admin solo
    /// elige el MarketHashName, no tipea nada a mano. Rechaza si el item no esta de verdad ahi, si
    /// no se puede cotizar, o si su precio supera el LootBox.MaxItemPrice de esa caja. El peso
    /// (probabilidad) se calcula solo, inversamente proporcional al precio: mas caro = menos
    /// probable, pero nunca 0.
    /// </summary>
    Task<string?> AddPoolItemFromStockAsync(string slug, string steamId64, string marketHashName, CancellationToken ct = default);
}
