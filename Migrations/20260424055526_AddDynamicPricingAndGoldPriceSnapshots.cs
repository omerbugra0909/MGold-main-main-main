using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MGold.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicPricingAndGoldPriceSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AdditionalCostSnapshot",
                table: "Transactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CalculatedPurchasePriceSnapshot",
                table: "Transactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CalculatedSalePriceSnapshot",
                table: "Transactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GoldPricePerGramSnapshot",
                table: "Transactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LaborCostPercentageSnapshot",
                table: "Transactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LaborCostSnapshot",
                table: "Transactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaterialCostSnapshot",
                table: "Transactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProductWeightSnapshot",
                table: "Transactions",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProfitMarginPercentageSnapshot",
                table: "Transactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PurityRateSnapshot",
                table: "Transactions",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCostSnapshot",
                table: "Transactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AdditionalCost",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LaborCost",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LaborCostPercentage",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProfitMarginPercentage",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PurityRate",
                table: "Products",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "GoldPrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PricePerGram = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldPrices", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoldPrices");

            migrationBuilder.DropColumn(
                name: "AdditionalCostSnapshot",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CalculatedPurchasePriceSnapshot",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CalculatedSalePriceSnapshot",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "GoldPricePerGramSnapshot",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "LaborCostPercentageSnapshot",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "LaborCostSnapshot",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "MaterialCostSnapshot",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ProductWeightSnapshot",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ProfitMarginPercentageSnapshot",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PurityRateSnapshot",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TotalCostSnapshot",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AdditionalCost",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LaborCost",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LaborCostPercentage",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ProfitMarginPercentage",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PurityRate",
                table: "Products");
        }
    }
}
