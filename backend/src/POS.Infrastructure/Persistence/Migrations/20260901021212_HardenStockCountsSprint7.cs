using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenStockCountsSprint7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockCounts_WarehouseId",
                table: "StockCounts");

            migrationBuilder.CreateIndex(
                name: "IX_StockCounts_WarehouseId",
                table: "StockCounts",
                column: "WarehouseId",
                unique: true,
                filter: "\"Status\" = 'Draft'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockCounts_WarehouseId",
                table: "StockCounts");

            migrationBuilder.CreateIndex(
                name: "IX_StockCounts_WarehouseId",
                table: "StockCounts",
                column: "WarehouseId");
        }
    }
}
