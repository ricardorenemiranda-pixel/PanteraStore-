namespace SteamMarket.Api.Contracts;

/// <summary>Body de POST /api/orders: los assetId que el usuario selecciono en su carrito.</summary>
public sealed class CreateSellOrderRequest
{
    public List<string> AssetIds { get; init; } = new();
}
