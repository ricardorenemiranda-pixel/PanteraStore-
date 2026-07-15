using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SteamMarket.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLootBoxes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "SellOrderId",
                table: "TradeOffers",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "TradeOffers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "LootBoxWinId",
                table: "TradeOffers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LootBoxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MaxItemPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LootBoxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LootBoxPoolItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LootBoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketHashName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Hero = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Slot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Rarity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Weight = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LootBoxPoolItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LootBoxPurchaseCounters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SteamId64 = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LootBoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LootBoxPurchaseCounters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LootBoxWins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LootBoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    PoolItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SteamId64 = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BotAssetId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WonAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LootBoxWins", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradeOffers_LootBoxWinId",
                table: "TradeOffers",
                column: "LootBoxWinId");

            migrationBuilder.CreateIndex(
                name: "IX_LootBoxes_Slug",
                table: "LootBoxes",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LootBoxPoolItems_LootBoxId",
                table: "LootBoxPoolItems",
                column: "LootBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_LootBoxPurchaseCounters_SteamId64_LootBoxId",
                table: "LootBoxPurchaseCounters",
                columns: new[] { "SteamId64", "LootBoxId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LootBoxWins_BotAssetId",
                table: "LootBoxWins",
                column: "BotAssetId",
                unique: true,
                filter: "\"Status\" IN ('Reserved', 'PendingRedeem')");

            migrationBuilder.CreateIndex(
                name: "IX_LootBoxWins_LootBoxId",
                table: "LootBoxWins",
                column: "LootBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_LootBoxWins_Status",
                table: "LootBoxWins",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LootBoxWins_SteamId64",
                table: "LootBoxWins",
                column: "SteamId64");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LootBoxes");

            migrationBuilder.DropTable(
                name: "LootBoxPoolItems");

            migrationBuilder.DropTable(
                name: "LootBoxPurchaseCounters");

            migrationBuilder.DropTable(
                name: "LootBoxWins");

            migrationBuilder.DropIndex(
                name: "IX_TradeOffers_LootBoxWinId",
                table: "TradeOffers");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "TradeOffers");

            migrationBuilder.DropColumn(
                name: "LootBoxWinId",
                table: "TradeOffers");

            migrationBuilder.AlterColumn<Guid>(
                name: "SellOrderId",
                table: "TradeOffers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
