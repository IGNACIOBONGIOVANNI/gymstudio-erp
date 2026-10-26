using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ObjetosDeBaseDeDatos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Datos heredados que violarían los CHECK nuevos (ver SqlObjetosDeBaseDeDatos.cs)
            migrationBuilder.Sql(SqlObjetosDeBaseDeDatos.CorregirDatos);

            migrationBuilder.CreateTable(
                name: "auditoria",
                schema: "contabilidad",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tabla = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    operacion = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    registro_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    datos_anteriores = table.Column<string>(type: "jsonb", nullable: true),
                    datos_nuevos = table.Column<string>(type: "jsonb", nullable: true),
                    campos_modificados = table.Column<string[]>(type: "text[]", nullable: false),
                    usuario = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auditoria", x => x.id);
                });

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

            migrationBuilder.AddCheckConstraint(
                name: "CK_Productos_StockNoNegativo",
                schema: "inventario",
                table: "Productos",
                sql: "\"StockActual\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Planes_Valores",
                schema: "contabilidad",
                table: "Planes",
                sql: "\"DuracionMeses\" > 0 AND \"PrecioMensual\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_movimientos_caja_importe_positivo",
                schema: "caja",
                table: "movimientos_caja",
                sql: "\"Importe\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_movimientos_banco_importe_positivo",
                schema: "banco",
                table: "movimientos_banco",
                sql: "\"Importe\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Membresias_Vigencia",
                schema: "contabilidad",
                table: "Membresias",
                sql: "\"FechaVencimiento\" >= \"FechaInicio\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_lineas_asiento_debe_o_haber",
                schema: "contabilidad",
                table: "lineas_asiento",
                sql: "(debe > 0) <> (haber > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_lineas_asiento_importes_no_negativos",
                schema: "contabilidad",
                table: "lineas_asiento",
                sql: "debe >= 0 AND haber >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_AsientoContableId",
                schema: "contabilidad",
                table: "Facturas",
                column: "AsientoContableId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Facturas_ImportesNoNegativos",
                schema: "contabilidad",
                table: "Facturas",
                sql: "\"Subtotal\" >= 0 AND \"Iva\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Facturas_TotalEsSubtotalMasIva",
                schema: "contabilidad",
                table: "Facturas",
                sql: "\"Total\" = \"Subtotal\" + \"Iva\"");

            migrationBuilder.CreateIndex(
                name: "IX_FacturaItems_ProductoId",
                schema: "contabilidad",
                table: "FacturaItems",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_auditoria_fecha",
                schema: "contabilidad",
                table: "auditoria",
                column: "fecha");

            migrationBuilder.CreateIndex(
                name: "IX_auditoria_tabla_registro_id",
                schema: "contabilidad",
                table: "auditoria",
                columns: new[] { "tabla", "registro_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_FacturaItems_Productos_ProductoId",
                schema: "contabilidad",
                table: "FacturaItems",
                column: "ProductoId",
                principalSchema: "inventario",
                principalTable: "Productos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Facturas_asientos_contables_AsientoContableId",
                schema: "contabilidad",
                table: "Facturas",
                column: "AsientoContableId",
                principalSchema: "contabilidad",
                principalTable: "asientos_contables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Triggers, vista y función (ver SqlObjetosDeBaseDeDatos.cs)
            migrationBuilder.Sql(SqlObjetosDeBaseDeDatos.Crear);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlObjetosDeBaseDeDatos.Eliminar);

            migrationBuilder.DropForeignKey(
                name: "FK_FacturaItems_Productos_ProductoId",
                schema: "contabilidad",
                table: "FacturaItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Facturas_asientos_contables_AsientoContableId",
                schema: "contabilidad",
                table: "Facturas");

            migrationBuilder.DropTable(
                name: "auditoria",
                schema: "contabilidad");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Productos_StockNoNegativo",
                schema: "inventario",
                table: "Productos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Planes_Valores",
                schema: "contabilidad",
                table: "Planes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_movimientos_caja_importe_positivo",
                schema: "caja",
                table: "movimientos_caja");

            migrationBuilder.DropCheckConstraint(
                name: "CK_movimientos_banco_importe_positivo",
                schema: "banco",
                table: "movimientos_banco");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Membresias_Vigencia",
                schema: "contabilidad",
                table: "Membresias");

            migrationBuilder.DropCheckConstraint(
                name: "CK_lineas_asiento_debe_o_haber",
                schema: "contabilidad",
                table: "lineas_asiento");

            migrationBuilder.DropCheckConstraint(
                name: "CK_lineas_asiento_importes_no_negativos",
                schema: "contabilidad",
                table: "lineas_asiento");

            migrationBuilder.DropIndex(
                name: "IX_Facturas_AsientoContableId",
                schema: "contabilidad",
                table: "Facturas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Facturas_ImportesNoNegativos",
                schema: "contabilidad",
                table: "Facturas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Facturas_TotalEsSubtotalMasIva",
                schema: "contabilidad",
                table: "Facturas");

            migrationBuilder.DropIndex(
                name: "IX_FacturaItems_ProductoId",
                schema: "contabilidad",
                table: "FacturaItems");

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
    }
}
