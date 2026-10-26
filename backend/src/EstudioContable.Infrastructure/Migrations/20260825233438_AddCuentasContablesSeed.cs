using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCuentasContablesSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "contabilidad",
                table: "CuentasContables",
                columns: new[] { "Id", "Activa", "ClienteId", "Codigo", "CuentaPadreId", "Naturaleza", "Nombre" },
                values: new object[,]
                {
                    { 102L, true, 1L, "1.1.1.02", null, 0, "Banco" },
                    { 103L, true, 1L, "1.1.2.01", null, 0, "Deudores por Ventas" },
                    { 105L, true, 1L, "1.1.2.02", null, 0, "IVA Crédito Fiscal" },
                    { 201L, true, 1L, "2.1.1.01", null, 1, "IVA Débito Fiscal" },
                    { 202L, true, 1L, "2.1.1.02", null, 1, "Proveedores" },
                    { 501L, true, 1L, "5.1.1.01", null, 3, "Compras" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "contabilidad",
                table: "CuentasContables",
                keyColumn: "Id",
                keyValue: 102L);

            migrationBuilder.DeleteData(
                schema: "contabilidad",
                table: "CuentasContables",
                keyColumn: "Id",
                keyValue: 103L);

            migrationBuilder.DeleteData(
                schema: "contabilidad",
                table: "CuentasContables",
                keyColumn: "Id",
                keyValue: 105L);

            migrationBuilder.DeleteData(
                schema: "contabilidad",
                table: "CuentasContables",
                keyColumn: "Id",
                keyValue: 201L);

            migrationBuilder.DeleteData(
                schema: "contabilidad",
                table: "CuentasContables",
                keyColumn: "Id",
                keyValue: 202L);

            migrationBuilder.DeleteData(
                schema: "contabilidad",
                table: "CuentasContables",
                keyColumn: "Id",
                keyValue: 501L);
        }
    }
}
