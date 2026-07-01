using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APDS.Migrations
{
    /// <inheritdoc />
    public partial class AddDelegate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DelegatedReviewerId",
                table: "Activities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastStatusChangeDate",
                table: "Activities",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_Activities_DelegatedReviewerId",
                table: "Activities",
                column: "DelegatedReviewerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_AspNetUsers_DelegatedReviewerId",
                table: "Activities",
                column: "DelegatedReviewerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_AspNetUsers_DelegatedReviewerId",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_DelegatedReviewerId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "DelegatedReviewerId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "LastStatusChangeDate",
                table: "Activities");
        }
    }
}
