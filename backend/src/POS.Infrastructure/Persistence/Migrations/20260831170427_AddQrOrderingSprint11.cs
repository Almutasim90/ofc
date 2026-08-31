using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQrOrderingSprint11 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "CashierUserId",
                table: "Orders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "CarPickupBays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    BayLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarPickupBays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CarPickupBays_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderingPoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    PointType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LinkedTableId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedCarBayId = table.Column<Guid>(type: "uuid", nullable: true),
                    QrCodeToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderingPoints", x => x.Id);
                    table.CheckConstraint("CK_OrderingPoints_Link", "(\"PointType\" = 'TABLE' AND \"LinkedTableId\" IS NOT NULL AND \"LinkedCarBayId\" IS NULL) OR (\"PointType\" = 'CAR_BAY' AND \"LinkedCarBayId\" IS NOT NULL AND \"LinkedTableId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_OrderingPoints_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderingPoints_CarPickupBays_LinkedCarBayId",
                        column: x => x.LinkedCarBayId,
                        principalTable: "CarPickupBays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderingPoints_Tables_LinkedTableId",
                        column: x => x.LinkedTableId,
                        principalTable: "Tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderingSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderingPointId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderingSessions", x => x.Id);
                    table.CheckConstraint("CK_OrderingSessions_Status", "\"Status\" IN ('Open','Closed')");
                    table.ForeignKey(
                        name: "FK_OrderingSessions_OrderingPoints_OrderingPointId",
                        column: x => x.OrderingPointId,
                        principalTable: "OrderingPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderingSessionId",
                table: "Orders",
                column: "OrderingSessionId",
                unique: true,
                filter: "\"OrderingSessionId\" IS NOT NULL AND \"Status\" = 'Open'");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SalesChannelId",
                table: "Orders",
                column: "SalesChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_CarPickupBays_BranchId_BayLabel",
                table: "CarPickupBays",
                columns: new[] { "BranchId", "BayLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderingPoints_BranchId",
                table: "OrderingPoints",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderingPoints_LinkedCarBayId",
                table: "OrderingPoints",
                column: "LinkedCarBayId",
                unique: true,
                filter: "\"LinkedCarBayId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrderingPoints_LinkedTableId",
                table: "OrderingPoints",
                column: "LinkedTableId",
                unique: true,
                filter: "\"LinkedTableId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrderingPoints_QrCodeToken",
                table: "OrderingPoints",
                column: "QrCodeToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderingSessions_OrderingPointId",
                table: "OrderingSessions",
                column: "OrderingPointId",
                unique: true,
                filter: "\"Status\" = 'Open'");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_OrderingSessions_OrderingSessionId",
                table: "Orders",
                column: "OrderingSessionId",
                principalTable: "OrderingSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_SalesChannels_SalesChannelId",
                table: "Orders",
                column: "SalesChannelId",
                principalTable: "SalesChannels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_OrderingSessions_OrderingSessionId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_SalesChannels_SalesChannelId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "OrderingSessions");

            migrationBuilder.DropTable(
                name: "OrderingPoints");

            migrationBuilder.DropTable(
                name: "CarPickupBays");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderingSessionId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_SalesChannelId",
                table: "Orders");

            migrationBuilder.AlterColumn<Guid>(
                name: "CashierUserId",
                table: "Orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
