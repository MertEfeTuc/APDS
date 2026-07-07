using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APDS.Migrations
{
    /// <inheritdoc />
    public partial class AddOrcidIntegration2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrcidId",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CitationCount",
                table: "Activities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Doi",
                table: "Activities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrcidPutCode",
                table: "Activities",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrcidId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CitationCount",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Doi",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "OrcidPutCode",
                table: "Activities");
        }
    }
}
