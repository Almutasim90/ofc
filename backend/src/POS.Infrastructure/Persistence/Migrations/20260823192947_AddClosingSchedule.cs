using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClosingSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClosingScheduleConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultCloseTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClosingScheduleConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClosingScheduleExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    OverrideCloseTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClosingScheduleExceptions", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ClosingScheduleConfigs",
                columns: new[] { "Id", "DefaultCloseTime", "IsActive" },
                values: new object[] { new Guid("b2b60295-51db-4ad0-aa5f-93a1c196a97f"), new TimeOnly(23, 45, 0), true });

            migrationBuilder.CreateIndex(
                name: "IX_ClosingScheduleExceptions_Date_BranchId",
                table: "ClosingScheduleExceptions",
                columns: new[] { "Date", "BranchId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClosingScheduleConfigs");

            migrationBuilder.DropTable(
                name: "ClosingScheduleExceptions");
        }
    }
}
