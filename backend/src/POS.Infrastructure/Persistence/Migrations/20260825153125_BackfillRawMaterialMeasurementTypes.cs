using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillRawMaterialMeasurementTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "RawMaterials"
                SET "MeasurementType" = CASE
                    WHEN lower("Unit") IN ('g', 'kg', 'gram', 'grams', 'جرام', 'كجم') THEN 'Weight'
                    WHEN lower("Unit") IN ('ml', 'l', 'liter', 'litre', 'مل', 'لتر') THEN 'Volume'
                    WHEN lower("Unit") IN ('piece', 'pieces', 'pcs', 'حبة', 'عدد') THEN 'Count'
                    ELSE 'Custom'
                END
                WHERE "MeasurementType" = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
