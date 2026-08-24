using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelsNotificationsAndAi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChannelId",
                table: "Sales",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("8680b68c-14f7-47e2-b9c8-5105da122ab9"));

            migrationBuilder.CreateTable(
                name: "AiInsightRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResultSummary = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInsightRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiProviderSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ApiKeyEncrypted = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProviderSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LowStockNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RawMaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggeredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LowStockNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LowStockNotifications_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LowStockNotifications_RawMaterials_RawMaterialId",
                        column: x => x.RawMaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NameAr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LogoUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsInStore = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesChannels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductChannelPrices",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductChannelPrices", x => new { x.ProductId, x.ChannelId });
                    table.ForeignKey(
                        name: "FK_ProductChannelPrices_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductChannelPrices_SalesChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "SalesChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "SalesChannels",
                columns: new[] { "Id", "NameAr", "NameEn", "LogoUrl", "IsActive", "IsInStore" },
                values: new object[] { new Guid("8680b68c-14f7-47e2-b9c8-5105da122ab9"), "المحل", "In-store", null!, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_ChannelId",
                table: "Sales",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_AiInsightRequests_CreatedAt",
                table: "AiInsightRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LowStockNotifications_BranchId_RawMaterialId",
                table: "LowStockNotifications",
                columns: new[] { "BranchId", "RawMaterialId" },
                unique: true,
                filter: "\"ResolvedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LowStockNotifications_RawMaterialId",
                table: "LowStockNotifications",
                column: "RawMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductChannelPrices_ChannelId",
                table: "ProductChannelPrices",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesChannels_IsInStore",
                table: "SalesChannels",
                column: "IsInStore",
                unique: true,
                filter: "\"IsInStore\" = TRUE");

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_SalesChannels_ChannelId",
                table: "Sales",
                column: "ChannelId",
                principalTable: "SalesChannels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sales_SalesChannels_ChannelId",
                table: "Sales");

            migrationBuilder.DropTable(
                name: "AiInsightRequests");

            migrationBuilder.DropTable(
                name: "AiProviderSettings");

            migrationBuilder.DropTable(
                name: "LowStockNotifications");

            migrationBuilder.DropTable(
                name: "ProductChannelPrices");

            migrationBuilder.DropTable(
                name: "SalesChannels");

            migrationBuilder.DropIndex(
                name: "IX_Sales_ChannelId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ChannelId",
                table: "Sales");
        }
    }
}
