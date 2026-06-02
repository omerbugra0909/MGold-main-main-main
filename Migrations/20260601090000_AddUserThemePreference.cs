using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MGold.Infrastructure.Data;

#nullable disable

namespace MGold.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260601090000_AddUserThemePreference")]
    public partial class AddUserThemePreference : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ThemePreference",
                table: "AppUsers",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "gold-premium");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ThemePreference",
                table: "AppUsers");
        }
    }
}
