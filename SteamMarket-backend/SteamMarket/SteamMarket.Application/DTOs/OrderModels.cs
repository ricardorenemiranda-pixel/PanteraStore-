namespace SteamMarket.Application.DTOs;

public sealed class SellOrderItemDto
{
    public string AssetId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? IconUrl { get; init; }
    public decimal PayoutPrice { get; init; }
}

public sealed class SellOrderDto
{
    public Guid Id { get; init; }
    public string SteamId { get; init; } = string.Empty;
    public IReadOnlyList<SellOrderItemDto> Items { get; init; } = Array.Empty<SellOrderItemDto>();
    public decimal TotalAmount { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ResolvedAtUtc { get; init; }
    public string? AdminNote { get; init; }
}

public sealed class CreateSellOrderResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public SellOrderDto? Order { get; init; }
    public string? AdminTradeUrl { get; init; }

    /// <summary>True si el bot le mando automaticamente la oferta de intercambio al usuario
    /// (solo tiene que abrir Steam y aceptarla). False si hubo que caer al flujo manual: el
    /// usuario tiene que abrir el AdminTradeUrl y armar el intercambio el mismo.</summary>
    public bool BotOfferSent { get; init; }

    /// <summary>Si BotOfferSent es true, la razon por la que no se pudo mandar automatico
    /// (bot desconectado, sin Trade URL guardada, etc.), para mostrarsela al usuario.</summary>
    public string? BotOfferError { get; init; }

    public static CreateSellOrderResult Ok(SellOrderDto order, string adminTradeUrl, bool botOfferSent = false, string? botOfferError = null) =>
        new() { Success = true, Order = order, AdminTradeUrl = adminTradeUrl, BotOfferSent = botOfferSent, BotOfferError = botOfferError };

    public static CreateSellOrderResult Fail(string error) => new() { Success = false, Error = error };
}
