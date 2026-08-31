using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WiseLine.Portal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPortalSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "integration");

            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.EnsureSchema(
                name: "auth");

            migrationBuilder.EnsureSchema(
                name: "portal");

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromotionCodes",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TrialExtensionDays = table.Column<int>(type: "int", nullable: false),
                    MaximumRedemptions = table.Column<int>(type: "int", nullable: true),
                    RedemptionCount = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebhookEvents",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProviderEventId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    ProcessingError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "auth",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvestmentIdentityLinks",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PortalUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TradeUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentIdentityLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvestmentIdentityLinks_Users_PortalUserId",
                        column: x => x.PortalUserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromotionRedemptions",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RedeemedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionRedemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromotionRedemptions_PromotionCodes_PromotionCodeId",
                        column: x => x.PromotionCodeId,
                        principalSchema: "billing",
                        principalTable: "PromotionCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PromotionRedemptions_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProviderCustomerId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ProviderSubscriptionId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TrialStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    TrialEndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    CurrentPeriodEndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    CanceledAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    CancelAtPeriodEnd = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserClaims",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                schema: "auth",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                schema: "portal",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "auth",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "auth",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                schema: "auth",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OccurredAt",
                schema: "audit",
                table: "AuditEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_UserId_OccurredAt",
                schema: "audit",
                table: "AuditEvents",
                columns: new[] { "UserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentIdentityLinks_PortalUserId",
                schema: "integration",
                table: "InvestmentIdentityLinks",
                column: "PortalUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentIdentityLinks_TradeUserId",
                schema: "integration",
                table: "InvestmentIdentityLinks",
                column: "TradeUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromotionCodes_Code",
                schema: "billing",
                table: "PromotionCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRedemptions_PromotionCodeId_UserId",
                schema: "billing",
                table: "PromotionRedemptions",
                columns: new[] { "PromotionCodeId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRedemptions_UserId",
                schema: "billing",
                table: "PromotionRedemptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                schema: "auth",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "auth",
                table: "Roles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_Provider_ProviderSubscriptionId",
                schema: "billing",
                table: "Subscriptions",
                columns: new[] { "Provider", "ProviderSubscriptionId" },
                unique: true,
                filter: "[ProviderSubscriptionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_UserId",
                schema: "billing",
                table: "Subscriptions",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserClaims_UserId",
                schema: "auth",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                schema: "auth",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "auth",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "auth",
                table: "Users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "auth",
                table: "Users",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEvents_Provider_ProviderEventId",
                schema: "billing",
                table: "WebhookEvents",
                columns: new[] { "Provider", "ProviderEventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "InvestmentIdentityLinks",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "PromotionRedemptions",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "RoleClaims",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "Subscriptions",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "UserClaims",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "UserLogins",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "UserProfiles",
                schema: "portal");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "UserTokens",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "WebhookEvents",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "PromotionCodes",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "auth");
        }
    }
}
