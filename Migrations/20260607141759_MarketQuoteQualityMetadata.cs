using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MGold.Migrations
{
    /// <inheritdoc />
    public partial class MarketQuoteQualityMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CalculationBasis",
                table: "MarketQuoteSnapshots",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataQualityStatus",
                table: "MarketQuoteSnapshots",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "ok");

            migrationBuilder.AddColumn<string>(
                name: "QualityWarningsJson",
                table: "MarketQuoteSnapshots",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "MarketQuoteSnapshots",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "live_market");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CalculationBasis",
                table: "MarketQuoteSnapshots");

            migrationBuilder.DropColumn(
                name: "DataQualityStatus",
                table: "MarketQuoteSnapshots");

            migrationBuilder.DropColumn(
                name: "QualityWarningsJson",
                table: "MarketQuoteSnapshots");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "MarketQuoteSnapshots");
        }
    }
}
