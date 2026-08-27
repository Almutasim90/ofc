using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleNumbering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SaleNumber",
                table: "Sales",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NextSaleNumber",
                table: "Branches",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Number every existing sale 1, 2, 3... per branch, oldest first, before the
            // unique index below can enforce it - a fresh install has no rows here, so this
            // is a no-op there.
            migrationBuilder.Sql("""
                WITH numbered AS (
                    SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "BranchId" ORDER BY "CreatedAt", "Id") AS "Rn"
                    FROM "Sales"
                )
                UPDATE "Sales" s SET "SaleNumber" = numbered."Rn"
                FROM numbered WHERE numbered."Id" = s."Id";

                UPDATE "Branches" b SET "NextSaleNumber" = COALESCE(
                    (SELECT MAX(s."SaleNumber") + 1 FROM "Sales" s WHERE s."BranchId" = b."Id"), 1);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_BranchId_SaleNumber",
                table: "Sales",
                columns: new[] { "BranchId", "SaleNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_BranchId_SaleNumber",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "SaleNumber",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "NextSaleNumber",
                table: "Branches");
        }
    }
}
