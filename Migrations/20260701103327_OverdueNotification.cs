using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APDS.Migrations
{
    /// <inheritdoc />
    public partial class OverdueNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OverdueNotificationSent",
                table: "Activities",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OverdueNotificationSent",
                table: "Activities");
        }
    }
}
