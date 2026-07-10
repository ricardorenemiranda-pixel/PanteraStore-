using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SteamMarket.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CachedInventories",
                columns: table => new
                {
                    SteamId64 = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ItemsJson = table.Column<string>(type: "TEXT", nullable: false),
                    FetchedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedInventories", x => x.SteamId64);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CachedInventories");
        }
    }
}
