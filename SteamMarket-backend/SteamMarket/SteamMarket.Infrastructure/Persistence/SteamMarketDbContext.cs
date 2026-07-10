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
            entity.Property(e => e.SteamId64).HasMaxLength(32);
            entity.HasIndex(e => e.SellOrderId);
            entity.HasIndex(e => e.Status);
        });
    }
}
