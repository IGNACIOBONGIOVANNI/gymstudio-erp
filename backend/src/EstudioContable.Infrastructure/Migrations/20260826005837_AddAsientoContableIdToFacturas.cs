using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAsientoContableIdToFacturas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AsientoContableId",
                schema: "ventas",
                table: "facturas_venta",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AsientoContableId",
                schema: "compras",
                table: "facturas_compra",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AsientoContableId",
                schema: "contabilidad",
                table: "Facturas",
                type: "bigint",
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "compras",
                table: "facturas_compra",
                keyColumn: "Id",
                keyValue: 1L,
                column: "AsientoContableId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "compras",
                table: "facturas_compra",
                keyColumn: "Id",
                keyValue: 2L,
                column: "AsientoContableId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "compras",
                table: "facturas_compra",
                keyColumn: "Id",
                keyValue: 3L,
                column: "AsientoContableId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "ventas",
                table: "facturas_venta",
                keyColumn: "Id",
                keyValue: 1L,
                column: "AsientoContableId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "ventas",
                table: "facturas_venta",
                keyColumn: "Id",
                keyValue: 2L,
                column: "AsientoContableId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "ventas",
                table: "facturas_venta",
                keyColumn: "Id",
                keyValue: 3L,
                column: "AsientoContableId",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AsientoContableId",
                schema: "ventas",
                table: "facturas_venta");

            migrationBuilder.DropColumn(
                name: "AsientoContableId",
                schema: "compras",
                table: "facturas_compra");

            migrationBuilder.DropColumn(
                name: "AsientoContableId",
                schema: "contabilidad",
                table: "Facturas");
        }
    }
}
