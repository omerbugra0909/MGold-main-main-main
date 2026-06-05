using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MGold.Infrastructure.Data;

#nullable disable

namespace MGold.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260601090100_AddPaymentProviderMetadata")]
    public partial class AddPaymentProviderMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderKey",
                table: "OrderPayments",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderTransactionId",
                table: "OrderPayments",
                type: "TEXT",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "OrderPayments",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InstallmentCount",
                table: "OrderPayments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresThreeDSecure",
                table: "OrderPayments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ThreeDSecureStatus",
                table: "OrderPayments",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentPaymentId",
                table: "OrderPayments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRefund",
                table: "OrderPayments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPartialRefund",
                table: "OrderPayments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                table: "OrderPayments",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureMessage",
                table: "OrderPayments",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderPayments_IdempotencyKey",
                table: "OrderPayments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderPayments_ParentPaymentId",
                table: "OrderPayments",
                column: "ParentPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPayments_ProviderKey_ProviderTransactionId",
                table: "OrderPayments",
                columns: new[] { "ProviderKey", "ProviderTransactionId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderPayments_OrderPayments_ParentPaymentId",
                table: "OrderPayments",
                column: "ParentPaymentId",
                principalTable: "OrderPayments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderPayments_OrderPayments_ParentPaymentId",
                table: "OrderPayments");

            migrationBuilder.DropIndex(
                name: "IX_OrderPayments_IdempotencyKey",
                table: "OrderPayments");

            migrationBuilder.DropIndex(
                name: "IX_OrderPayments_ParentPaymentId",
                table: "OrderPayments");

            migrationBuilder.DropIndex(
                name: "IX_OrderPayments_ProviderKey_ProviderTransactionId",
                table: "OrderPayments");

            migrationBuilder.DropColumn(name: "FailureCode", table: "OrderPayments");
            migrationBuilder.DropColumn(name: "FailureMessage", table: "OrderPayments");
            migrationBuilder.DropColumn(name: "IdempotencyKey", table: "OrderPayments");
            migrationBuilder.DropColumn(name: "InstallmentCount", table: "OrderPayments");
            migrationBuilder.DropColumn(name: "IsPartialRefund", table: "OrderPayments");
            migrationBuilder.DropColumn(name: "IsRefund", table: "OrderPayments");
            migrationBuilder.DropColumn(name: "ParentPaymentId", table: "OrderPayments");
            migrationBuilder.DropColumn(name: "ProviderKey", table: "OrderPayments");
            migrationBuilder.DropColumn(name: "ProviderTransactionId", table: "OrderPayments");
            migrationBuilder.DropColumn(name: "RequiresThreeDSecure", table: "OrderPayments");
            migrationBuilder.DropColumn(name: "ThreeDSecureStatus", table: "OrderPayments");
        }
    }
}
