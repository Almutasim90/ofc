using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrintingSprint9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrinterConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    NameAr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrinterConfigs", x => x.Id);
                    table.CheckConstraint("CK_PrinterConfigs_Port", "\"Port\" BETWEEN 1 AND 65535");
                    table.ForeignKey(
                        name: "FK_PrinterConfigs_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrinterSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    NameAr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PrinterConfigId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrinterSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrinterSections_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PrinterSections_PrinterConfigs_PrinterConfigId",
                        column: x => x.PrinterConfigId,
                        principalTable: "PrinterConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_PrinterSectionId",
                table: "MenuItems",
                column: "PrinterSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_PrinterConfigs_BranchId",
                table: "PrinterConfigs",
                column: "BranchId",
                unique: true,
                filter: "\"IsDefault\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_PrinterConfigs_BranchId_NameEn",
                table: "PrinterConfigs",
                columns: new[] { "BranchId", "NameEn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrinterSections_BranchId_NameEn",
                table: "PrinterSections",
                columns: new[] { "BranchId", "NameEn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrinterSections_PrinterConfigId",
                table: "PrinterSections",
                column: "PrinterConfigId");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_PrinterSections_PrinterSectionId",
                table: "MenuItems",
                column: "PrinterSectionId",
                principalTable: "PrinterSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_PrinterSections_PrinterSectionId",
                table: "MenuItems");

            migrationBuilder.DropTable(
                name: "PrinterSections");

            migrationBuilder.DropTable(
                name: "PrinterConfigs");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_PrinterSectionId",
                table: "MenuItems");
        }
    }
}
