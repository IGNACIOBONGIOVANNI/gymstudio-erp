using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMockData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "compras",
                table: "facturas_compra",
                keyColumn: "Id",
                keyValue: 1L);

            migrationBuilder.DeleteData(
                schema: "compras",
                table: "facturas_compra",
                keyColumn: "Id",
                keyValue: 2L);

            migrationBuilder.DeleteData(
                schema: "compras",
                table: "facturas_compra",
                keyColumn: "Id",
                keyValue: 3L);

            migrationBuilder.DeleteData(
                schema: "ventas",
                table: "facturas_venta",
                keyColumn: "Id",
                keyValue: 1L);

            migrationBuilder.DeleteData(
                schema: "ventas",
                table: "facturas_venta",
                keyColumn: "Id",
                keyValue: 2L);

            migrationBuilder.DeleteData(
                schema: "ventas",
                table: "facturas_venta",
                keyColumn: "Id",
                keyValue: 3L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "compras",
                table: "facturas_compra",
                columns: new[] { "Id", "AsientoContableId", "cliente_id", "CondicionPago", "cuit", "fecha", "iva", "neto", "numero_factura", "proveedor_nombre", "tipo_factura", "total" },
                values: new object[,]
                {
                    { 1L, null, 1L, 0, "XX", new DateTime(2026, 6, 5, 0, 0, 0, 0, DateTimeKind.Utc), 2100m, 10000m, "0001-00", "Tornillo", "A", 12100m },
                    { 2L, null, 1L, 0, "XX", new DateTime(2026, 6, 5, 0, 0, 0, 0, DateTimeKind.Utc), 4200m, 20000m, "0002-00", "Tuerca", "A", 24200m },
                    { 3L, null, 1L, 0, "XX", new DateTime(2026, 6, 5, 0, 0, 0, 0, DateTimeKind.Utc), 840m, 4000m, "0003-00", "Arandelas", "A", 4840m }
                });

            migrationBuilder.InsertData(
                schema: "ventas",
                table: "facturas_venta",
                columns: new[] { "Id", "AsientoContableId", "cliente_id", "cliente_nombre", "CondicionPago", "cuit", "fecha", "iva", "neto", "numero_factura", "tipo_factura", "total" },
                values: new object[,]
                {
                    { 1L, null, 1L, "Destornilladores Juego", 0, "XX", new DateTime(2026, 5, 15, 0, 0, 0, 0, DateTimeKind.Utc), 8820m, 42000m, "0001-00", "A", 50820m },
                    { 2L, null, 1L, "Buscapolo", 0, "XX", new DateTime(2026, 5, 15, 0, 0, 0, 0, DateTimeKind.Utc), 12600m, 60000m, "0002-00", "A", 72600m },
                    { 3L, null, 1L, "soldadora mig", 0, "XX", new DateTime(2026, 5, 15, 0, 0, 0, 0, DateTimeKind.Utc), 285600m, 1360000m, "0003-00", "A", 1645600m }
                });
        }
    }
}
