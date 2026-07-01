using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APDS.Migrations
{
    /// <inheritdoc />
    public partial class AddDelegate2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PendingDelegationReviewerId",
                table: "Activities",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Activities_PendingDelegationReviewerId",
                table: "Activities",
                column: "PendingDelegationReviewerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_AspNetUsers_PendingDelegationReviewerId",
                table: "Activities",
                column: "PendingDelegationReviewerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_AspNetUsers_PendingDelegationReviewerId",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_PendingDelegationReviewerId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "PendingDelegationReviewerId",
                table: "Activities");
        }
    }
}
