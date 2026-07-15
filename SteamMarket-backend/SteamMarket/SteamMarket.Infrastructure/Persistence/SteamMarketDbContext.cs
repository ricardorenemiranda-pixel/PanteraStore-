using Microsoft.EntityFrameworkCore;
using SteamMarket.Infrastructure.Pricing.Models;

namespace SteamMarket.Infrastructure.Persistence;

/// <summary>
/// DbContext propio, minimo por ahora: solo guarda la cache de precios de mercado.
/// Usa SQLite en dev (ver DependencyInjection.AddInfrastructure).
/// </summary>
public sealed class SteamMarketDbContext : DbContext
{
    public SteamMarketDbContext(DbContextOptions<SteamMarketDbContext> options) : base(options)
    {
    }

    public DbSet<CachedMarketPrice> MarketPrices => Set<CachedMarketPrice>();
    public DbSet<CachedInventory> Inventories => Set<CachedInventory>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<SellOrder> SellOrders => Set<SellOrder>();
    public DbSet<WithdrawalRequest> WithdrawalRequests => Set<WithdrawalRequest>();
    public DbSet<AdminAccount> AdminAccounts => Set<AdminAccount>();
    public DbSet<TradeOffer> TradeOffers => Set<TradeOffer>();
    public DbSet<LootBox> LootBoxes => Set<LootBox>();
    public DbSet<LootBoxPoolItem> LootBoxPoolItems => Set<LootBoxPoolItem>();
    public DbSet<LootBoxWin> LootBoxWins => Set<LootBoxWin>();
    public DbSet<LootBoxPurchaseCounter> LootBoxPurchaseCounters => Set<LootBoxPurchaseCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CachedMarketPrice>(entity =>
        {
            entity.ToTable("MarketPrices");
            entity.HasKey(e => e.MarketHashName);
            entity.Property(e => e.MarketHashName).HasMaxLength(300);
            entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<CachedInventory>(entity =>
        {
            entity.ToTable("CachedInventories");
            entity.HasKey(e => e.SteamId64);
            entity.Property(e => e.SteamId64).HasMaxLength(32);
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.ToTable("UserProfiles");
            entity.HasKey(e => e.SteamId64);
            entity.Property(e => e.SteamId64).HasMaxLength(32);
            entity.Property(e => e.TradeUrl).HasMaxLength(500);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.Phone).HasMaxLength(30);
            entity.Property(e => e.DocumentType).HasMaxLength(20);
            entity.Property(e => e.DocumentNumber).HasMaxLength(20);
            entity.Property(e => e.Balance).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<WalletTransaction>(entity =>
        {
            entity.ToTable("WalletTransactions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SteamId64).HasMaxLength(32);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Type).HasMaxLength(30);
            entity.Property(e => e.RelatedId).HasMaxLength(64);
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.HasIndex(e => e.SteamId64);
        });

        modelBuilder.Entity<SellOrder>(entity =>
        {
            entity.ToTable("SellOrders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SteamId64).HasMaxLength(32);
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.AdminNote).HasMaxLength(500);
            entity.HasIndex(e => e.SteamId64);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<WithdrawalRequest>(entity =>
        {
            entity.ToTable("WithdrawalRequests");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SteamId64).HasMaxLength(32);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Method).HasMaxLength(30);
            entity.Property(e => e.Destination).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.HasIndex(e => e.SteamId64);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<AdminAccount>(entity =>
        {
            entity.ToTable("AdminAccounts");
            entity.HasKey(e => e.SteamId64);
            entity.Property(e => e.SteamId64).HasMaxLength(32);
            entity.Property(e => e.TradeUrl).HasMaxLength(500);
            entity.Property(e => e.Label).HasMaxLength(100);
        });

        modelBuilder.Entity<TradeOffer>(entity =>
        {
            entity.ToTable("TradeOffers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Kind).HasMaxLength(20);
            entity.Property(e => e.SteamId64).HasMaxLength(32);
            entity.HasIndex(e => e.SellOrderId);
            entity.HasIndex(e => e.LootBoxWinId);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<LootBox>(entity =>
        {
            entity.ToTable("LootBoxes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Slug).HasMaxLength(100);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.Category).HasMaxLength(30);
            entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
            entity.Property(e => e.MaxItemPrice).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.HasIndex(e => e.Slug).IsUnique();
        });

        modelBuilder.Entity<LootBoxPoolItem>(entity =>
        {
            entity.ToTable("LootBoxPoolItems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MarketHashName).HasMaxLength(300);
            entity.Property(e => e.DisplayName).HasMaxLength(300);
            entity.Property(e => e.Hero).HasMaxLength(100);
            entity.Property(e => e.Slot).HasMaxLength(50);
            entity.Property(e => e.Type).HasMaxLength(20);
            entity.Property(e => e.Rarity).HasMaxLength(30);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.HasIndex(e => e.LootBoxId);
        });

        modelBuilder.Entity<LootBoxWin>(entity =>
        {
            entity.ToTable("LootBoxWins");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SteamId64).HasMaxLength(32);
            entity.Property(e => e.BotAssetId).HasMaxLength(64);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.HasIndex(e => e.SteamId64);
            entity.HasIndex(e => e.LootBoxId);
            entity.HasIndex(e => e.Status);

            // Un mismo item real del bot no puede quedar reservado/en canje para dos ganadores a
            // la vez: el indice es la barrera anti-condicion-de-carrera cuando dos usuarios abren
            // cajas al mismo tiempo y ambos sortean el mismo MarketHashName (ver LootBoxService).
            // Es un indice PARCIAL (solo sobre filas activas) a proposito: una vez que un premio
            // queda "Sold" o "Redeemed", ese AssetId puede volver a aparecer en un LootBoxWin
            // futuro (si se vendio, el item sigue de verdad en el inventario del bot y puede
            // volver a tocarle a otro usuario).
            entity.HasIndex(e => e.BotAssetId)
                .IsUnique()
                .HasFilter("\"Status\" IN ('Reserved', 'PendingRedeem')");
        });

        modelBuilder.Entity<LootBoxPurchaseCounter>(entity =>
        {
            entity.ToTable("LootBoxPurchaseCounters");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SteamId64).HasMaxLength(32);
            entity.HasIndex(e => new { e.SteamId64, e.LootBoxId }).IsUnique();
        });
    }
}
