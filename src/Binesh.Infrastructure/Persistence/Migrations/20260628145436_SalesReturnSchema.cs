using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Binesh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesReturnSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sales_returns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Price = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<float>(type: "real", nullable: false),
                    DeliveredQuantity = table.Column<float>(type: "real", nullable: false),
                    DocNumber = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    CounterpartyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_returns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_returns_customers_CounterpartyId",
                        column: x => x.CounterpartyId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_returns_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sales_returns_counterparty",
                table: "sales_returns",
                column: "CounterpartyId");

            migrationBuilder.CreateIndex(
                name: "ix_sales_returns_date",
                table: "sales_returns",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "ix_sales_returns_product",
                table: "sales_returns",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_returns");
        }
    }
}
