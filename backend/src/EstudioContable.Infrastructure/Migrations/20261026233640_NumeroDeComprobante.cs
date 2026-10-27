using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NumeroDeComprobante : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "NumeroComprobante",
                schema: "contabilidad",
                table: "Facturas",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PuntoVenta",
                schema: "contabilidad",
                table: "Facturas",
                type: "integer",
                nullable: true);

            // Las ventas ya cargadas reciben número correlativo (punto de venta 1) en orden
            // de fecha; las compras viejas quedan sin número porque no se conoce.
            migrationBuilder.Sql("""
                UPDATE contabilidad."Facturas" f
                SET "PuntoVenta" = 1, "NumeroComprobante" = n.numero
                FROM (SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "TipoComprobante" ORDER BY "Fecha", "Id") AS numero
                      FROM contabilidad."Facturas" WHERE "TipoOperacion" = 'Venta') n
                WHERE f."Id" = n."Id";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE contabilidad."Facturas" ADD CONSTRAINT "CK_Facturas_Numeracion" CHECK (
                    ("PuntoVenta" IS NULL OR "PuntoVenta" BETWEEN 1 AND 99999)
                    AND ("NumeroComprobante" IS NULL OR "NumeroComprobante" BETWEEN 1 AND 99999999));

                -- Una venta no puede repetir número dentro de su tipo y punto de venta.
                CREATE UNIQUE INDEX "UX_Facturas_NumeroVenta" ON contabilidad."Facturas"
                    ("TipoComprobante", "PuntoVenta", "NumeroComprobante")
                    WHERE "TipoOperacion" = 'Venta' AND "NumeroComprobante" IS NOT NULL;

                -- La misma factura de un proveedor no puede estar cargada dos veces (salvo anulada).
                CREATE UNIQUE INDEX "UX_Facturas_NumeroCompra" ON contabilidad."Facturas"
                    ("ProveedorId", "TipoComprobante", "PuntoVenta", "NumeroComprobante")
                    WHERE "TipoOperacion" = 'Compra' AND NOT "Anulada"
                      AND "ProveedorId" IS NOT NULL AND "NumeroComprobante" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS contabilidad."UX_Facturas_NumeroCompra";
                DROP INDEX IF EXISTS contabilidad."UX_Facturas_NumeroVenta";
                ALTER TABLE contabilidad."Facturas" DROP CONSTRAINT IF EXISTS "CK_Facturas_Numeracion";
                """);

            migrationBuilder.DropColumn(
                name: "NumeroComprobante",
                schema: "contabilidad",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "PuntoVenta",
                schema: "contabilidad",
                table: "Facturas");
        }
    }
}
