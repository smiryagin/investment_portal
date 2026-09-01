using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WiseLine.Portal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTradeEntitlementOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TradeEntitlementSyncRequests",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PortalUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeEntitlementSyncRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeEntitlementSyncRequests_Users_PortalUserId",
                        column: x => x.PortalUserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradeEntitlementSyncRequests_PortalUserId_RequestedAt",
                schema: "integration",
                table: "TradeEntitlementSyncRequests",
                columns: new[] { "PortalUserId", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TradeEntitlementSyncRequests_ProcessedAt_NextAttemptAt",
                schema: "integration",
                table: "TradeEntitlementSyncRequests",
                columns: new[] { "ProcessedAt", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradeEntitlementSyncRequests",
                schema: "integration");
        }
    }
}
