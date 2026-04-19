using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsureZenAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReviewerId",
                table: "Claims",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Claims_CompanyId_SubmittedAt",
                table: "Claims",
                columns: new[] { "CompanyId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_ReviewerId",
                table: "Claims",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_Status",
                table: "Claims",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_SubmittedAt",
                table: "Claims",
                column: "SubmittedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_Claims_Users_ReviewerId",
                table: "Claims",
                column: "ReviewerId",
                principalTable: "Users",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Claims_Users_ReviewerId",
                table: "Claims");

            migrationBuilder.DropIndex(
                name: "IX_Claims_CompanyId_SubmittedAt",
                table: "Claims");

            migrationBuilder.DropIndex(
                name: "IX_Claims_ReviewerId",
                table: "Claims");

            migrationBuilder.DropIndex(
                name: "IX_Claims_Status",
                table: "Claims");

            migrationBuilder.DropIndex(
                name: "IX_Claims_SubmittedAt",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ReviewerId",
                table: "Claims");
        }
    }
}
