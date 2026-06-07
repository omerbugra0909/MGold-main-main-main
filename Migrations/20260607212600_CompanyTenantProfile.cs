using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MGold.Infrastructure.Data;

#nullable disable

namespace MGold.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260607212600_CompanyTenantProfile")]
    public partial class CompanyTenantProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>("Categories", "Companies", type: "nvarchar(300)", maxLength: 300, nullable: true);
            migrationBuilder.AddColumn<string>("City", "Companies", type: "nvarchar(80)", maxLength: 80, nullable: true);
            migrationBuilder.AddColumn<string>("CoverImageUrl", "Companies", type: "nvarchar(260)", maxLength: 260, nullable: true);
            migrationBuilder.AddColumn<string>("Description", "Companies", type: "nvarchar(600)", maxLength: 600, nullable: true);
            migrationBuilder.AddColumn<string>("District", "Companies", type: "nvarchar(80)", maxLength: 80, nullable: true);
            migrationBuilder.AddColumn<string>("LogoUrl", "Companies", type: "nvarchar(260)", maxLength: 260, nullable: true);
            migrationBuilder.AddColumn<string>("SearchKeywords", "Companies", type: "nvarchar(300)", maxLength: 300, nullable: true);
            migrationBuilder.AddColumn<string>("SocialLinks", "Companies", type: "nvarchar(400)", maxLength: 400, nullable: true);
            migrationBuilder.AddColumn<string>("TaxNumber", "Companies", type: "nvarchar(40)", maxLength: 40, nullable: true);
            migrationBuilder.AddColumn<string>("TaxOffice", "Companies", type: "nvarchar(120)", maxLength: 120, nullable: true);
            migrationBuilder.AddColumn<string>("WebsiteUrl", "Companies", type: "nvarchar(180)", maxLength: 180, nullable: true);
            migrationBuilder.AddColumn<string>("WorkingHours", "Companies", type: "nvarchar(600)", maxLength: 600, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("Categories", "Companies");
            migrationBuilder.DropColumn("City", "Companies");
            migrationBuilder.DropColumn("CoverImageUrl", "Companies");
            migrationBuilder.DropColumn("Description", "Companies");
            migrationBuilder.DropColumn("District", "Companies");
            migrationBuilder.DropColumn("LogoUrl", "Companies");
            migrationBuilder.DropColumn("SearchKeywords", "Companies");
            migrationBuilder.DropColumn("SocialLinks", "Companies");
            migrationBuilder.DropColumn("TaxNumber", "Companies");
            migrationBuilder.DropColumn("TaxOffice", "Companies");
            migrationBuilder.DropColumn("WebsiteUrl", "Companies");
            migrationBuilder.DropColumn("WorkingHours", "Companies");
        }
    }
}
