using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMixedPaymentsAndSaleEdits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SalesRevision",
                table: "Shifts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CardAmount",
                table: "Sales",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CashAmount",
                table: "Sales",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Sales",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RecipeSnapshotJson",
                table: "SaleItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "BranchRawMaterialStocks",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "SaleEdits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    BeforeJson = table.Column<string>(type: "text", nullable: false),
                    AfterJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleEdits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleEdits_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaleEdits_SaleId_CreatedAt",
                table: "SaleEdits",
                columns: new[] { "SaleId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaleEdits");

            migrationBuilder.DropColumn(
                name: "SalesRevision",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "CardAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CashAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "RecipeSnapshotJson",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "BranchRawMaterialStocks");
        }
    }
}
