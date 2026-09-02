using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workplace.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkCalendarSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkCalendarSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SyncedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EventsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkCalendarSnapshots", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkCalendarSnapshots");
        }
    }
}
