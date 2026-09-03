using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workplace.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "CalendarColorSettings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "CalendarColorSettings");
        }
    }
}
