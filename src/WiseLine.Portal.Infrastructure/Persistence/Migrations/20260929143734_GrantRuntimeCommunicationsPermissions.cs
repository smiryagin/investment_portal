using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WiseLine.Portal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrantRuntimeCommunicationsPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF USER_ID(N'WiseLinePortal_Runtime') IS NOT NULL
                BEGIN
                    GRANT SELECT, INSERT, UPDATE ON SCHEMA::[communications]
                        TO [WiseLinePortal_Runtime];
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF USER_ID(N'WiseLinePortal_Runtime') IS NOT NULL
                BEGIN
                    REVOKE SELECT, INSERT, UPDATE ON SCHEMA::[communications]
                        FROM [WiseLinePortal_Runtime];
                END;
                """);
        }
    }
}
