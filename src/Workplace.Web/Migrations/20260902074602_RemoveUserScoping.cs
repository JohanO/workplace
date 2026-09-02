using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workplace.Web.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUserScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppUsers");

            migrationBuilder.DropIndex(
                name: "IX_ConnectedAccounts_UserId_Provider_ProviderAccountId",
                table: "ConnectedAccounts");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ConnectedAccounts");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectedAccounts_Provider_ProviderAccountId",
                table: "ConnectedAccounts",
                columns: new[] { "Provider", "ProviderAccountId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConnectedAccounts_Provider_ProviderAccountId",
                table: "ConnectedAccounts");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "ConnectedAccounts",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AppUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectedAccounts_UserId_Provider_ProviderAccountId",
                table: "ConnectedAccounts",
                columns: new[] { "UserId", "Provider", "ProviderAccountId" },
                unique: true);
        }
    }
}
