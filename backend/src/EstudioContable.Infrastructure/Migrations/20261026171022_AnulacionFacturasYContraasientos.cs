using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AnulacionFacturasYContraasientos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Anulada",
                schema: "contabilidad",
                table: "Facturas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAnulacion",
                schema: "contabilidad",
                table: "Facturas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProductoId",
                schema: "contabilidad",
                table: "FacturaItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "asiento_revertido_id",
                schema: "contabilidad",
                table: "asientos_contables",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_asientos_contables_asiento_revertido_id",
                schema: "contabilidad",
                table: "asientos_contables",
                column: "asiento_revertido_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_asientos_contables_asientos_contables_asiento_revertido_id",
                schema: "contabilidad",
                table: "asientos_contables",
                column: "asiento_revertido_id",
                principalSchema: "contabilidad",
                principalTable: "asientos_contables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_asientos_contables_asientos_contables_asiento_revertido_id",
                schema: "contabilidad",
                table: "asientos_contables");

            migrationBuilder.DropIndex(
                name: "IX_asientos_contables_asiento_revertido_id",
                schema: "contabilidad",
                table: "asientos_contables");

            migrationBuilder.DropColumn(
                name: "Anulada",
                schema: "contabilidad",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "FechaAnulacion",
                schema: "contabilidad",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "ProductoId",
                schema: "contabilidad",
                table: "FacturaItems");

            migrationBuilder.DropColumn(
                name: "asiento_revertido_id",
                schema: "contabilidad",
                table: "asientos_contables");
        }
    }
}
