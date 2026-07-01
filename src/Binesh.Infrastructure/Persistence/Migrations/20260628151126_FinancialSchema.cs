using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Binesh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinancialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "financial_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Debit = table.Column<long>(type: "bigint", nullable: false),
                    Credit = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_mapping_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OperationalCost = table.Column<string>(type: "jsonb", nullable: false),
                    Payables = table.Column<string>(type: "jsonb", nullable: false),
                    ToCalculateSales = table.Column<string>(type: "jsonb", nullable: false),
                    ToCalculateLiquidity = table.Column<string>(type: "jsonb", nullable: false),
                    ToCalculateGrossProfitLoss = table.Column<string>(type: "jsonb", nullable: false),
                    ToCalculateOperatingProfitLoss = table.Column<string>(type: "jsonb", nullable: false),
                    ToCalculateProfitLossBeforTax = table.Column<string>(type: "jsonb", nullable: false),
                    ToCalculateNetProfitLoss = table.Column<string>(type: "jsonb", nullable: false),
                    ToCalculateAccumulatedProfitLoss = table.Column<string>(type: "jsonb", nullable: false),
                    ToCalculateEquity = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_mapping_settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_financial_entries_code",
                table: "financial_entries",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "ix_financial_entries_type",
                table: "financial_entries",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financial_entries");

            migrationBuilder.DropTable(
                name: "financial_mapping_settings");
        }
    }
}
