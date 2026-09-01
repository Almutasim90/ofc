using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenQrOrderingSprint11 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderingSessionId",
                table: "Orders");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByUserId",
                table: "InventoryTransactions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderingSessionId",
                table: "Orders",
                column: "OrderingSessionId",
                unique: true,
                filter: "\"OrderingSessionId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderingSessionId",
                table: "Orders");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByUserId",
                table: "InventoryTransactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderingSessionId",
                table: "Orders",
                column: "OrderingSessionId",
                unique: true,
                filter: "\"OrderingSessionId\" IS NOT NULL AND \"Status\" = 'Open'");
        }
    }
}
