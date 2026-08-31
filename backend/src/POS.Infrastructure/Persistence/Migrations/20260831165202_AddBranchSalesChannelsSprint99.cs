using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchSalesChannelsSprint99 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "SalesChannels",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.Sql("UPDATE \"SalesChannels\" SET \"Code\" = CASE WHEN \"IsInStore\" THEN 'IN_STORE' ELSE 'CHANNEL_' || REPLACE(\"Id\"::text, '-', '') END");
            migrationBuilder.AlterColumn<string>(name: "Code", table: "SalesChannels", type: "character varying(60)", maxLength: 60, nullable: false, oldClrType: typeof(string), oldType: "character varying(60)", oldMaxLength: 60, oldNullable: true);

            migrationBuilder.CreateTable(
                name: "BranchSalesChannelAvailabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresPrepayment = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchSalesChannelAvailabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchSalesChannelAvailabilities_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BranchSalesChannelAvailabilities_SalesChannels_SalesChannel~",
                        column: x => x.SalesChannelId,
                        principalTable: "SalesChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesChannels_Code",
                table: "SalesChannels",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BranchSalesChannelAvailabilities_BranchId_SalesChannelId",
                table: "BranchSalesChannelAvailabilities",
                columns: new[] { "BranchId", "SalesChannelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BranchSalesChannelAvailabilities_SalesChannelId",
                table: "BranchSalesChannelAvailabilities",
                column: "SalesChannelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BranchSalesChannelAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_SalesChannels_Code",
                table: "SalesChannels");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "SalesChannels");
        }
    }
}
