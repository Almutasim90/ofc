using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttributePaymentsToCashShiftsSprint975 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CashShiftId",
                table: "OrderPayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderPayments_CashShiftId",
                table: "OrderPayments",
                column: "CashShiftId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderPayments_CashShifts_CashShiftId",
                table: "OrderPayments",
                column: "CashShiftId",
                principalTable: "CashShifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderPayments_CashShifts_CashShiftId",
                table: "OrderPayments");

            migrationBuilder.DropIndex(
                name: "IX_OrderPayments_CashShiftId",
                table: "OrderPayments");

            migrationBuilder.DropColumn(
                name: "CashShiftId",
                table: "OrderPayments");
        }
    }
}
