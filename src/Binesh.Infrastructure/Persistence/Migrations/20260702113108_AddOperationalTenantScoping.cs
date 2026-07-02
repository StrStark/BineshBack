using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Binesh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalTenantScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sales_returns_date",
                table: "sales_returns");

            migrationBuilder.DropIndex(
                name: "ix_sales_date",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "ix_products_code",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_type",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_financial_entries_code",
                table: "financial_entries");

            migrationBuilder.DropIndex(
                name: "ix_financial_entries_type",
                table: "financial_entries");

            migrationBuilder.DropIndex(
                name: "ix_customers_type",
                table: "customers");

            migrationBuilder.RenameIndex(
                name: "ix_sales_returns_product",
                table: "sales_returns",
                newName: "IX_sales_returns_ProductId");

            migrationBuilder.RenameIndex(
                name: "ix_sales_returns_counterparty",
                table: "sales_returns",
                newName: "IX_sales_returns_CounterpartyId");

            migrationBuilder.RenameIndex(
                name: "ix_sales_product",
                table: "sales",
                newName: "IX_sales_ProductId");

            migrationBuilder.RenameIndex(
                name: "ix_sales_counterparty",
                table: "sales",
                newName: "IX_sales_CounterpartyId");

            migrationBuilder.Sql("""
                INSERT INTO companies ("Id", "Name", "Slug", "Status", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), 'Binesh', 'binesh', 'active', NOW(), NOW()
                WHERE NOT EXISTS (SELECT 1 FROM companies WHERE "Slug" = 'binesh');
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "sales_returns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "financial_mapping_settings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "financial_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE sales_returns
                SET "CompanyId" = (SELECT "Id" FROM companies WHERE "Slug" = 'binesh' LIMIT 1)
                WHERE "CompanyId" IS NULL;

                UPDATE sales
                SET "CompanyId" = (SELECT "Id" FROM companies WHERE "Slug" = 'binesh' LIMIT 1)
                WHERE "CompanyId" IS NULL;

                UPDATE products
                SET "CompanyId" = (SELECT "Id" FROM companies WHERE "Slug" = 'binesh' LIMIT 1)
                WHERE "CompanyId" IS NULL;

                UPDATE financial_mapping_settings
                SET "CompanyId" = (SELECT "Id" FROM companies WHERE "Slug" = 'binesh' LIMIT 1)
                WHERE "CompanyId" IS NULL;

                UPDATE financial_entries
                SET "CompanyId" = (SELECT "Id" FROM companies WHERE "Slug" = 'binesh' LIMIT 1)
                WHERE "CompanyId" IS NULL;

                UPDATE customers
                SET "CompanyId" = (SELECT "Id" FROM companies WHERE "Slug" = 'binesh' LIMIT 1)
                WHERE "CompanyId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                table: "sales_returns",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                table: "sales",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                table: "products",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                table: "financial_mapping_settings",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                table: "financial_entries",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                table: "customers",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_returns_company_counterparty",
                table: "sales_returns",
                columns: new[] { "CompanyId", "CounterpartyId" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_returns_company_date",
                table: "sales_returns",
                columns: new[] { "CompanyId", "Date" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_returns_company_product",
                table: "sales_returns",
                columns: new[] { "CompanyId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_company_counterparty",
                table: "sales",
                columns: new[] { "CompanyId", "CounterpartyId" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_company_date",
                table: "sales",
                columns: new[] { "CompanyId", "Date" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_company_product",
                table: "sales",
                columns: new[] { "CompanyId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "ix_products_company_code",
                table: "products",
                columns: new[] { "CompanyId", "ProductCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_products_company_type",
                table: "products",
                columns: new[] { "CompanyId", "Type" });

            migrationBuilder.CreateIndex(
                name: "ux_financial_mapping_settings_company",
                table: "financial_mapping_settings",
                column: "CompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_entries_company_code",
                table: "financial_entries",
                columns: new[] { "CompanyId", "Code" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_entries_company_type",
                table: "financial_entries",
                columns: new[] { "CompanyId", "Type" });

            migrationBuilder.CreateIndex(
                name: "ix_customers_company_active",
                table: "customers",
                columns: new[] { "CompanyId", "Active" });

            migrationBuilder.CreateIndex(
                name: "ix_customers_company_type",
                table: "customers",
                columns: new[] { "CompanyId", "Type" });

            migrationBuilder.AddForeignKey(
                name: "FK_customers_companies_CompanyId",
                table: "customers",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_financial_entries_companies_CompanyId",
                table: "financial_entries",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_financial_mapping_settings_companies_CompanyId",
                table: "financial_mapping_settings",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_companies_CompanyId",
                table: "products",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sales_companies_CompanyId",
                table: "sales",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sales_returns_companies_CompanyId",
                table: "sales_returns",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_customers_companies_CompanyId",
                table: "customers");

            migrationBuilder.DropForeignKey(
                name: "FK_financial_entries_companies_CompanyId",
                table: "financial_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_financial_mapping_settings_companies_CompanyId",
                table: "financial_mapping_settings");

            migrationBuilder.DropForeignKey(
                name: "FK_products_companies_CompanyId",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_sales_companies_CompanyId",
                table: "sales");

            migrationBuilder.DropForeignKey(
                name: "FK_sales_returns_companies_CompanyId",
                table: "sales_returns");

            migrationBuilder.DropIndex(
                name: "ix_sales_returns_company_counterparty",
                table: "sales_returns");

            migrationBuilder.DropIndex(
                name: "ix_sales_returns_company_date",
                table: "sales_returns");

            migrationBuilder.DropIndex(
                name: "ix_sales_returns_company_product",
                table: "sales_returns");

            migrationBuilder.DropIndex(
                name: "ix_sales_company_counterparty",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "ix_sales_company_date",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "ix_sales_company_product",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "ix_products_company_code",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_company_type",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ux_financial_mapping_settings_company",
                table: "financial_mapping_settings");

            migrationBuilder.DropIndex(
                name: "ix_financial_entries_company_code",
                table: "financial_entries");

            migrationBuilder.DropIndex(
                name: "ix_financial_entries_company_type",
                table: "financial_entries");

            migrationBuilder.DropIndex(
                name: "ix_customers_company_active",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "ix_customers_company_type",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "sales_returns");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "products");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "financial_mapping_settings");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "financial_entries");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "customers");

            migrationBuilder.RenameIndex(
                name: "IX_sales_returns_ProductId",
                table: "sales_returns",
                newName: "ix_sales_returns_product");

            migrationBuilder.RenameIndex(
                name: "IX_sales_returns_CounterpartyId",
                table: "sales_returns",
                newName: "ix_sales_returns_counterparty");

            migrationBuilder.RenameIndex(
                name: "IX_sales_ProductId",
                table: "sales",
                newName: "ix_sales_product");

            migrationBuilder.RenameIndex(
                name: "IX_sales_CounterpartyId",
                table: "sales",
                newName: "ix_sales_counterparty");

            migrationBuilder.CreateIndex(
                name: "ix_sales_returns_date",
                table: "sales_returns",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "ix_sales_date",
                table: "sales",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "ix_products_code",
                table: "products",
                column: "ProductCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_products_type",
                table: "products",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "ix_financial_entries_code",
                table: "financial_entries",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "ix_financial_entries_type",
                table: "financial_entries",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "ix_customers_type",
                table: "customers",
                column: "Type");
        }
    }
}
