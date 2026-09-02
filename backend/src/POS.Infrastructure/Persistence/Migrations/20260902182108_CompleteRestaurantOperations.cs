using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteRestaurantOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Status",
                table: "Orders");

            migrationBuilder.AddColumn<Guid>(
                name: "FloorId",
                table: "Tables",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PositionX",
                table: "Tables",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PositionY",
                table: "Tables",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Shape",
                table: "Tables",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Rectangle");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceAddressAr",
                table: "Orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceAddressEn",
                table: "Orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceCommercialRegistrationNumber",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceCurrency",
                table: "Orders",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InvoiceDefaultTaxRate",
                table: "Orders",
                type: "numeric(7,3)",
                precision: 7,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InvoiceDiscountSnapshot",
                table: "Orders",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceFooter",
                table: "Orders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InvoiceGrandTotalSnapshot",
                table: "Orders",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceLegalNameAr",
                table: "Orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceLegalNameEn",
                table: "Orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoicePhone",
                table: "Orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InvoicePricesIncludeTax",
                table: "Orders",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvoiceSnapshotCapturedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InvoiceSubtotalSnapshot",
                table: "Orders",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceTaxRegistrationNumber",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InvoiceTaxSnapshot",
                table: "Orders",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BillSplitId",
                table: "OrderPayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InvoiceGrossSnapshot",
                table: "OrderItems",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InvoiceNetSnapshot",
                table: "OrderItems",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InvoiceTaxRateSnapshot",
                table: "OrderItems",
                type: "numeric(7,3)",
                precision: 7,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InvoiceTaxSnapshot",
                table: "OrderItems",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BillSplits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillSplits", x => x.Id);
                    table.CheckConstraint("CK_BillSplits_Amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_BillSplits_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BranchQrOrderingSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    OpensAt = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    ClosesAt = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchQrOrderingSchedules", x => x.Id);
                    table.CheckConstraint("CK_BranchQrOrderingSchedules_Day", "\"DayOfWeek\" BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "FK_BranchQrOrderingSchedules_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalNameAr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LegalNameEn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TaxRegistrationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CommercialRegistrationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AddressAr = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AddressEn = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PricesIncludeTax = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultTaxRate = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: false),
                    Footer = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceSettings_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RestaurantFloors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantFloors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestaurantFloors_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BillSplitLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BillSplitId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillSplitLines", x => x.Id);
                    table.CheckConstraint("CK_BillSplitLines_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_BillSplitLines_BillSplits_BillSplitId",
                        column: x => x.BillSplitId,
                        principalTable: "BillSplits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BillSplitLines_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO "RestaurantFloors" ("Id", "BranchId", "Name", "SortOrder", "IsActive")
                SELECT gen_random_uuid(), b."Id", 'Main', 0, TRUE
                FROM "Branches" b
                WHERE NOT EXISTS (SELECT 1 FROM "RestaurantFloors" f WHERE f."BranchId" = b."Id");
                UPDATE "Tables" t
                SET "FloorId" = (SELECT f."Id" FROM "RestaurantFloors" f WHERE f."BranchId" = t."BranchId" ORDER BY f."SortOrder", f."Id" LIMIT 1)
                WHERE t."FloorId" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Tables_FloorId",
                table: "Tables",
                column: "FloorId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tables_Position",
                table: "Tables",
                sql: "\"PositionX\" BETWEEN 0 AND 100 AND \"PositionY\" BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tables_Shape",
                table: "Tables",
                sql: "\"Shape\" IN ('Rectangle','Round')");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BranchId_Status_CreatedAt",
                table: "Orders",
                columns: new[] { "BranchId", "Status", "CreatedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Status",
                table: "Orders",
                sql: "\"Status\" IN ('Open','PendingApproval','Sent','Paid','Closed','Cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPayments_BillSplitId",
                table: "OrderPayments",
                column: "BillSplitId");

            migrationBuilder.CreateIndex(
                name: "IX_BillSplitLines_BillSplitId_OrderItemId",
                table: "BillSplitLines",
                columns: new[] { "BillSplitId", "OrderItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillSplitLines_OrderItemId",
                table: "BillSplitLines",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BillSplits_OrderId_CreatedAt",
                table: "BillSplits",
                columns: new[] { "OrderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BranchQrOrderingSchedules_BranchId_DayOfWeek",
                table: "BranchQrOrderingSchedules",
                columns: new[] { "BranchId", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSettings_BranchId",
                table: "InvoiceSettings",
                column: "BranchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantFloors_BranchId_Name",
                table: "RestaurantFloors",
                columns: new[] { "BranchId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantFloors_BranchId_SortOrder",
                table: "RestaurantFloors",
                columns: new[] { "BranchId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_OrderPayments_BillSplits_BillSplitId",
                table: "OrderPayments",
                column: "BillSplitId",
                principalTable: "BillSplits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tables_RestaurantFloors_FloorId",
                table: "Tables",
                column: "FloorId",
                principalTable: "RestaurantFloors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderPayments_BillSplits_BillSplitId",
                table: "OrderPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_Tables_RestaurantFloors_FloorId",
                table: "Tables");

            migrationBuilder.DropTable(
                name: "BillSplitLines");

            migrationBuilder.DropTable(
                name: "BranchQrOrderingSchedules");

            migrationBuilder.DropTable(
                name: "InvoiceSettings");

            migrationBuilder.DropTable(
                name: "RestaurantFloors");

            migrationBuilder.DropTable(
                name: "BillSplits");

            migrationBuilder.DropIndex(
                name: "IX_Tables_FloorId",
                table: "Tables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Tables_Position",
                table: "Tables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Tables_Shape",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_Orders_BranchId_Status_CreatedAt",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Status",
                table: "Orders");

            migrationBuilder.Sql("UPDATE \"Orders\" SET \"Status\" = 'Open' WHERE \"Status\" = 'PendingApproval'");

            migrationBuilder.DropIndex(
                name: "IX_OrderPayments_BillSplitId",
                table: "OrderPayments");

            migrationBuilder.DropColumn(
                name: "FloorId",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "PositionX",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "PositionY",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "Shape",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceAddressAr",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceAddressEn",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceCommercialRegistrationNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceCurrency",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceDefaultTaxRate",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceDiscountSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceFooter",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceGrandTotalSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceLegalNameAr",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceLegalNameEn",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoicePhone",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoicePricesIncludeTax",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceSnapshotCapturedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceSubtotalSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceTaxRegistrationNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceTaxSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BillSplitId",
                table: "OrderPayments");

            migrationBuilder.DropColumn(
                name: "InvoiceGrossSnapshot",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "InvoiceNetSnapshot",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "InvoiceTaxRateSnapshot",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "InvoiceTaxSnapshot",
                table: "OrderItems");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Status",
                table: "Orders",
                sql: "\"Status\" IN ('Open','Sent','Paid','Closed','Cancelled')");
        }
    }
}
