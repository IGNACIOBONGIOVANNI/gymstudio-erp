using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCondicionPagoToFacturas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CondicionPago",
                schema: "ventas",
                table: "facturas_venta",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CondicionPago",
                schema: "compras",
                table: "facturas_compra",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                schema: "compras",
                table: "facturas_compra",
                keyColumn: "Id",
                keyValue: 1L,
                column: "CondicionPago",
                value: 0);

            migrationBuilder.UpdateData(
                schema: "compras",
                table: "facturas_compra",
                keyColumn: "Id",
                keyValue: 2L,
                column: "CondicionPago",
                value: 0);

            migrationBuilder.UpdateData(
                schema: "compras",
                table: "facturas_compra",
                keyColumn: "Id",
                keyValue: 3L,
                column: "CondicionPago",
                value: 0);

            migrationBuilder.UpdateData(
                schema: "ventas",
                table: "facturas_venta",
                keyColumn: "Id",
                keyValue: 1L,
                column: "CondicionPago",
                value: 0);

            migrationBuilder.UpdateData(
                schema: "ventas",
                table: "facturas_venta",
                keyColumn: "Id",
                keyValue: 2L,
                column: "CondicionPago",
                value: 0);

            migrationBuilder.UpdateData(
                schema: "ventas",
                table: "facturas_venta",
                keyColumn: "Id",
                keyValue: 3L,
                column: "CondicionPago",
                value: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CondicionPago",
                schema: "ventas",
                table: "facturas_venta");

            migrationBuilder.DropColumn(
                name: "CondicionPago",
                schema: "compras",
                table: "facturas_compra");
        }
    }
}
