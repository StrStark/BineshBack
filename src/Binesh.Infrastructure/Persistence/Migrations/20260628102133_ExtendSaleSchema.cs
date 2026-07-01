using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Binesh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendSaleSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CounterpartyId",
                table: "sales",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<float>(
                name: "DeliveredQuantity",
                table: "sales",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                table: "sales",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_sales_counterparty",
                table: "sales",
                column: "CounterpartyId");

            migrationBuilder.CreateIndex(
                name: "ix_sales_product",
                table: "sales",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_sales_customers_CounterpartyId",
                table: "sales",
                column: "CounterpartyId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sales_products_ProductId",
                table: "sales",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sales_customers_CounterpartyId",
                table: "sales");

            migrationBuilder.DropForeignKey(
                name: "FK_sales_products_ProductId",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "ix_sales_counterparty",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "ix_sales_product",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "CounterpartyId",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "DeliveredQuantity",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "sales");
        }
    }
}
