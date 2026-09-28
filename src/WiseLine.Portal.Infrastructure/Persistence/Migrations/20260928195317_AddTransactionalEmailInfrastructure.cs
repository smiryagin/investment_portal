using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WiseLine.Portal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionalEmailInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "communications");

            migrationBuilder.CreateTable(
                name: "EmailOutbox",
                schema: "communications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    TemplateKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HtmlBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TextBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ProcessingToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LockedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailOutbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailOutbox_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EmailWebhookEvents",
                schema: "communications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderEventId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    PayloadSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailWebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_IdempotencyKey",
                schema: "communications",
                table: "EmailOutbox",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_ProviderMessageId",
                schema: "communications",
                table: "EmailOutbox",
                column: "ProviderMessageId",
                unique: true,
                filter: "[ProviderMessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_Status_NextAttemptAt_LockedUntil",
                schema: "communications",
                table: "EmailOutbox",
                columns: new[] { "Status", "NextAttemptAt", "LockedUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_UserId",
                schema: "communications",
                table: "EmailOutbox",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailWebhookEvents_ProviderEventId",
                schema: "communications",
                table: "EmailWebhookEvents",
                column: "ProviderEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailWebhookEvents_ProviderMessageId_OccurredAt",
                schema: "communications",
                table: "EmailWebhookEvents",
                columns: new[] { "ProviderMessageId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailOutbox",
                schema: "communications");

            migrationBuilder.DropTable(
                name: "EmailWebhookEvents",
                schema: "communications");
        }
    }
}
