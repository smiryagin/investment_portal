using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WiseLine.Portal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignTradeIdentifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [integration].[InvestmentIdentityLinks])
                    THROW 51001, 'Trade identifier conversion requires an empty InvestmentIdentityLinks table.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_InvestmentIdentityLinks_TradeUserId",
                schema: "integration",
                table: "InvestmentIdentityLinks");

            migrationBuilder.DropColumn(
                name: "TradeUserId",
                schema: "integration",
                table: "InvestmentIdentityLinks");

            migrationBuilder.AddColumn<Guid>(
                name: "TradeUserId",
                schema: "integration",
                table: "InvestmentIdentityLinks",
                type: "uniqueidentifier",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentIdentityLinks_TradeUserId",
                schema: "integration",
                table: "InvestmentIdentityLinks",
                column: "TradeUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [integration].[InvestmentIdentityLinks])
                    THROW 51002, 'Trade identifier rollback requires an empty InvestmentIdentityLinks table.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_InvestmentIdentityLinks_TradeUserId",
                schema: "integration",
                table: "InvestmentIdentityLinks");

            migrationBuilder.DropColumn(
                name: "TradeUserId",
                schema: "integration",
                table: "InvestmentIdentityLinks");

            migrationBuilder.AddColumn<long>(
                name: "TradeUserId",
                schema: "integration",
                table: "InvestmentIdentityLinks",
                type: "bigint",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentIdentityLinks_TradeUserId",
                schema: "integration",
                table: "InvestmentIdentityLinks",
                column: "TradeUserId",
                unique: true);
        }
    }
}
