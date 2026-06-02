using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MGold.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketModuleAndRealtimePricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketProviderConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProviderKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupportsRealtime = table.Column<bool>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    RefreshIntervalSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ApiKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LastSuccessfulSyncAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastFailureAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    FailureCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketProviderConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketQuoteSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitLabel = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    NativeCurrency = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    PriceInUsd = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    Price24hAgoInUsd = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    High24hInUsd = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    Low24hInUsd = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    SparklineJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    IsFallback = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketQuoteSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketWatchlistItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketWatchlistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketWatchlistItems_AppUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketProviderConfigurations_IsEnabled_Priority",
                table: "MarketProviderConfigurations",
                columns: new[] { "IsEnabled", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketProviderConfigurations_ProviderKey",
                table: "MarketProviderConfigurations",
                column: "ProviderKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketQuoteSnapshots_Category",
                table: "MarketQuoteSnapshots",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_MarketQuoteSnapshots_LastUpdatedAt",
                table: "MarketQuoteSnapshots",
                column: "LastUpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MarketQuoteSnapshots_Symbol",
                table: "MarketQuoteSnapshots",
                column: "Symbol",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketWatchlistItems_AppUserId_Symbol",
                table: "MarketWatchlistItems",
                columns: new[] { "AppUserId", "Symbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketWatchlistItems_CreatedAt",
                table: "MarketWatchlistItems",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketProviderConfigurations");

            migrationBuilder.DropTable(
                name: "MarketQuoteSnapshots");

            migrationBuilder.DropTable(
                name: "MarketWatchlistItems");
        }
    }
}
