using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MGold.Migrations
{
    public partial class AddRefreshTokens : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql("""
                    CREATE TABLE IF NOT EXISTS RefreshTokens (
                        Id INTEGER NOT NULL CONSTRAINT PK_RefreshTokens PRIMARY KEY AUTOINCREMENT,
                        AppUserId INTEGER NOT NULL,
                        TokenHash TEXT NOT NULL,
                        SecurityStampSnapshot TEXT NOT NULL,
                        DeviceName TEXT NULL,
                        CreatedByIp TEXT NULL,
                        CreatedByUserAgent TEXT NULL,
                        CreatedAt TEXT NOT NULL,
                        ExpiresAt TEXT NOT NULL,
                        RevokedAt TEXT NULL,
                        RevokedByIp TEXT NULL,
                        ReplacedByTokenHash TEXT NULL,
                        CONSTRAINT FK_RefreshTokens_AppUsers_AppUserId FOREIGN KEY (AppUserId) REFERENCES AppUsers (Id) ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS IX_RefreshTokens_TokenHash ON RefreshTokens (TokenHash);
                    CREATE INDEX IF NOT EXISTS IX_RefreshTokens_AppUserId_ExpiresAt_RevokedAt ON RefreshTokens (AppUserId, ExpiresAt, RevokedAt);
                """);
                return;
            }

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppUserId = table.Column<int>(nullable: false),
                    TokenHash = table.Column<string>(maxLength: 128, nullable: false),
                    SecurityStampSnapshot = table.Column<string>(maxLength: 128, nullable: false),
                    DeviceName = table.Column<string>(maxLength: 160, nullable: true),
                    CreatedByIp = table.Column<string>(maxLength: 80, nullable: true),
                    CreatedByUserAgent = table.Column<string>(maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    ExpiresAt = table.Column<DateTime>(nullable: false),
                    RevokedAt = table.Column<DateTime>(nullable: true),
                    RevokedByIp = table.Column<string>(maxLength: 80, nullable: true),
                    ReplacedByTokenHash = table.Column<string>(maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_AppUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_AppUserId_ExpiresAt_RevokedAt",
                table: "RefreshTokens",
                columns: new[] { "AppUserId", "ExpiresAt", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefreshTokens");
        }
    }
}
