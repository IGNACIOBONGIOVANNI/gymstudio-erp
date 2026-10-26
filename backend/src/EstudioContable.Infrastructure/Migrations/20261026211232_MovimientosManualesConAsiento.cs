using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MovimientosManualesConAsiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AsientoContableId",
                schema: "caja",
                table: "movimientos_caja",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AsientoContableId",
                schema: "banco",
                table: "movimientos_banco",
                type: "bigint",
                nullable: true);

            migrationBuilder.InsertData(
                schema: "contabilidad",
                table: "CuentasContables",
                columns: new[] { "Id", "Activa", "ClienteId", "Codigo", "CuentaPadreId", "Naturaleza", "Nombre" },
                values: new object[,]
                {
                    { 106L, true, 1L, "4.1.2.01", null, 3, "Otros Ingresos" },
                    { 107L, true, 1L, "5.2.1.01", null, 3, "Gastos Generales" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_caja_AsientoContableId",
                schema: "caja",
                table: "movimientos_caja",
                column: "AsientoContableId");

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_banco_AsientoContableId",
                schema: "banco",
                table: "movimientos_banco",
                column: "AsientoContableId");

            migrationBuilder.AddForeignKey(
                name: "FK_movimientos_banco_asientos_contables_AsientoContableId",
                schema: "banco",
                table: "movimientos_banco",
                column: "AsientoContableId",
                principalSchema: "contabilidad",
                principalTable: "asientos_contables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_movimientos_caja_asientos_contables_AsientoContableId",
                schema: "caja",
                table: "movimientos_caja",
                column: "AsientoContableId",
                principalSchema: "contabilidad",
                principalTable: "asientos_contables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_movimientos_banco_asientos_contables_AsientoContableId",
                schema: "banco",
                table: "movimientos_banco");

            migrationBuilder.DropForeignKey(
                name: "FK_movimientos_caja_asientos_contables_AsientoContableId",
                schema: "caja",
                table: "movimientos_caja");

            migrationBuilder.DropIndex(
                name: "IX_movimientos_caja_AsientoContableId",
                schema: "caja",
                table: "movimientos_caja");

            migrationBuilder.DropIndex(
                name: "IX_movimientos_banco_AsientoContableId",
                schema: "banco",
                table: "movimientos_banco");

            migrationBuilder.DeleteData(
                schema: "contabilidad",
                table: "CuentasContables",
                keyColumn: "Id",
                keyValue: 106L);

            migrationBuilder.DeleteData(
                schema: "contabilidad",
                table: "CuentasContables",
                keyColumn: "Id",
                keyValue: 107L);

            migrationBuilder.DropColumn(
                name: "AsientoContableId",
                schema: "caja",
                table: "movimientos_caja");

            migrationBuilder.DropColumn(
                name: "AsientoContableId",
                schema: "banco",
                table: "movimientos_banco");
        }
    }
}
