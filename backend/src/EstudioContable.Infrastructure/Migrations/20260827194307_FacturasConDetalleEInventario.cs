using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FacturasConDetalleEInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FacturaDetalles",
                schema: "contabilidad",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacturaVentaId = table.Column<long>(type: "bigint", nullable: true),
                    FacturaCompraId = table.Column<long>(type: "bigint", nullable: true),
                    ProductoId = table.Column<long>(type: "bigint", nullable: false),
                    Cantidad = table.Column<int>(type: "integer", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "numeric", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacturaDetalles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacturaDetalles_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalSchema: "inventario",
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FacturaDetalles_facturas_compra_FacturaCompraId",
                        column: x => x.FacturaCompraId,
                        principalSchema: "compras",
                        principalTable: "facturas_compra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FacturaDetalles_facturas_venta_FacturaVentaId",
                        column: x => x.FacturaVentaId,
                        principalSchema: "ventas",
                        principalTable: "facturas_venta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "inventario",
                table: "Productos",
                columns: new[] { "Id", "Nombre", "PrecioUnitario", "PrecioVenta", "StockActual" },
                values: new object[,]
                {
                    { 1L, "Producto A", 100m, 150m, 100 },
                    { 2L, "Producto B", 200m, 300m, 50 },
                    { 3L, "Servicio C", 0m, 500m, 0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_FacturaDetalles_FacturaCompraId",
                schema: "contabilidad",
                table: "FacturaDetalles",
                column: "FacturaCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturaDetalles_FacturaVentaId",
                schema: "contabilidad",
                table: "FacturaDetalles",
                column: "FacturaVentaId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturaDetalles_ProductoId",
                schema: "contabilidad",
                table: "FacturaDetalles",
                column: "ProductoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FacturaDetalles",
                schema: "contabilidad");

            migrationBuilder.DeleteData(
                schema: "inventario",
                table: "Productos",
                keyColumn: "Id",
                keyValue: 1L);

            migrationBuilder.DeleteData(
                schema: "inventario",
                table: "Productos",
                keyColumn: "Id",
                keyValue: 2L);

            migrationBuilder.DeleteData(
                schema: "inventario",
                table: "Productos",
                keyColumn: "Id",
                keyValue: 3L);
        }
    }
}
