using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleConsumptionSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SaleInventoryConsumptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RawMaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityConsumed = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleInventoryConsumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleInventoryConsumptions_RawMaterials_RawMaterialId",
                        column: x => x.RawMaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleInventoryConsumptions_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaleInventoryConsumptions_RawMaterialId",
                table: "SaleInventoryConsumptions",
                column: "RawMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleInventoryConsumptions_SaleId",
                table: "SaleInventoryConsumptions",
                column: "SaleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaleInventoryConsumptions");
        }
    }
}
