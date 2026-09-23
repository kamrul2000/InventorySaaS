using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySaaS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseCUniqueIndexesAndDeletedByFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_TenantId_Name",
                table: "UnitsOfMeasure",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId_Code",
                table: "Suppliers",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[Code] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_Code",
                table: "Customers",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[Code] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UnitsOfMeasure_TenantId_Name",
                table: "UnitsOfMeasure");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_TenantId_Code",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId_Code",
                table: "Customers");
        }
    }
}
