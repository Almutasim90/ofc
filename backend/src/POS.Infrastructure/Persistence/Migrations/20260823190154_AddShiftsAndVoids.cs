using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftsAndVoids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashierUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpeningCash = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ClosingCashExpected = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ClosingCashActual = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    VarianceAmount = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AutoClosed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.Id);
                });

            // Preserve any Sprint 3 sales by assigning each one to a closed legacy shift.
            // Fresh installations have no rows, so this is a no-op there.
            migrationBuilder.Sql("""
                INSERT INTO "Shifts" (
                    "Id", "BranchId", "CashierUserId", "OpeningCash", "ClosingCashExpected",
                    "ClosingCashActual", "VarianceAmount", "OpenedAt", "ClosedAt", "Status", "AutoClosed")
                SELECT "Id", "BranchId", "CashierUserId", 0,
                    CASE WHEN "PaymentMethod" = 'Cash' AND "Status" = 'Completed' THEN "TotalAmount" ELSE 0 END,
                    CASE WHEN "PaymentMethod" = 'Cash' AND "Status" = 'Completed' THEN "TotalAmount" ELSE 0 END,
                    0, "CreatedAt", "CreatedAt", 'Closed', FALSE
                FROM "Sales"
                WHERE "ShiftId" IS NULL;

                UPDATE "Sales" SET "ShiftId" = "Id" WHERE "ShiftId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "ShiftId",
                table: "Sales",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "VoidRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoidRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoidRequests_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_ShiftId",
                table: "Sales",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_BranchId_OpenedAt",
                table: "Shifts",
                columns: new[] { "BranchId", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_CashierUserId_Status",
                table: "Shifts",
                columns: new[] { "CashierUserId", "Status" });

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_Shifts_OneOpenPerCashier"
                ON "Shifts" ("CashierUserId")
                WHERE "Status" = 'Open';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_VoidRequests_SaleId",
                table: "VoidRequests",
                column: "SaleId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Shifts_ShiftId",
                table: "Sales",
                column: "ShiftId",
                principalTable: "Shifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Shifts_ShiftId",
                table: "Sales");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropTable(
                name: "VoidRequests");

            migrationBuilder.DropIndex(
                name: "IX_Sales_ShiftId",
                table: "Sales");

            migrationBuilder.AlterColumn<Guid>(
                name: "ShiftId",
                table: "Sales",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }
    }
}
