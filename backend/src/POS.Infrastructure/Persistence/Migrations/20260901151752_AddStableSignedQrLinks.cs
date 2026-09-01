using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStableSignedQrLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessToken",
                table: "OrderingSessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValueSql: "md5(random()::text || clock_timestamp()::text) || md5(random()::text || clock_timestamp()::text)");

            migrationBuilder.AddColumn<int>(
                name: "QrTokenVersion",
                table: "OrderingPoints",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("""
                UPDATE "OrderingSessions"
                SET "AccessToken" = md5("Id"::text || random()::text || clock_timestamp()::text)
                    || md5(random()::text || "OpenedAt"::text || "Id"::text)
                WHERE "AccessToken" = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_OrderingSessions_AccessToken",
                table: "OrderingSessions",
                column: "AccessToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderingSessions_AccessToken",
                table: "OrderingSessions");

            migrationBuilder.DropColumn(
                name: "AccessToken",
                table: "OrderingSessions");

            migrationBuilder.DropColumn(
                name: "QrTokenVersion",
                table: "OrderingPoints");
        }
    }
}
