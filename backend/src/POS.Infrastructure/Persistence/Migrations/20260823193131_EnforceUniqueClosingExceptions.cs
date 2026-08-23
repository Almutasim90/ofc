using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueClosingExceptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClosingScheduleExceptions_Date_BranchId",
                table: "ClosingScheduleExceptions");

            migrationBuilder.CreateIndex(
                name: "IX_ClosingScheduleExceptions_Date",
                table: "ClosingScheduleExceptions",
                column: "Date",
                unique: true,
                filter: "\"BranchId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClosingScheduleExceptions_Date_BranchId",
                table: "ClosingScheduleExceptions",
                columns: new[] { "Date", "BranchId" },
                unique: true,
                filter: "\"BranchId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClosingScheduleExceptions_Date",
                table: "ClosingScheduleExceptions");

            migrationBuilder.DropIndex(
                name: "IX_ClosingScheduleExceptions_Date_BranchId",
                table: "ClosingScheduleExceptions");

            migrationBuilder.CreateIndex(
                name: "IX_ClosingScheduleExceptions_Date_BranchId",
                table: "ClosingScheduleExceptions",
                columns: new[] { "Date", "BranchId" },
                unique: true);
        }
    }
}
