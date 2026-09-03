using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workplace.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarColorSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarColorSettings",
                columns: table => new
                {
                    CalendarKey = table.Column<string>(type: "TEXT", nullable: false),
                    Color = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarColorSettings", x => x.CalendarKey);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarColorSettings");
        }
    }
}
