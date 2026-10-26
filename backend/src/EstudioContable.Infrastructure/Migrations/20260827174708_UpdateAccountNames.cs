using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAccountNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "contabilidad",
                table: "CuentasContables",
                keyColumn: "Id",
                keyValue: 105L,
                column: "Nombre",
                value: "1.1.2.01 - IVA Crédito Fiscal");

            migrationBuilder.UpdateData(
                schema: "contabilidad",
                table: "CuentasContables",
                keyColumn: "Id",
                keyValue: 201L,
                column: "Nombre",
                value: "2.1.1.01 - IVA Débito Fiscal");

            migrationBuilder.UpdateData(
                schema: "contabilidad",
                table: "CuentasContables",
                keyColumn: "Id",
                keyValue: 501L,
                column: "Nombre",
                value: "5.1.1.01 - Compras");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "contabilidad",
                table: "CuentasContables",
                keyColumn: "Id",
                keyValue: 105L,
                column: "Nombre",
                value: "IVA Crédito Fiscal");

            migrationBuilder.UpdateData(
                schema: "contabilidad",
                table: "CuentasContables",
                keyColumn: "Id",
                keyValue: 201L,
                column: "Nombre",
                value: "IVA Débito Fiscal");

            migrationBuilder.UpdateData(
                schema: "contabilidad",
                table: "CuentasContables",
                keyColumn: "Id",
                keyValue: 501L,
                column: "Nombre",
                value: "Compras");
        }
    }
}
