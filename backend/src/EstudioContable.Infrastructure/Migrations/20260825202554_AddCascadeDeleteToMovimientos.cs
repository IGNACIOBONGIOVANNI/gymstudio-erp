using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCascadeDeleteToMovimientos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_movimientos_caja_FacturaId",
                schema: "caja",
                table: "movimientos_caja",
                column: "FacturaId");

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_banco_FacturaId",
                schema: "banco",
                table: "movimientos_banco",
                column: "FacturaId");

            migrationBuilder.AddForeignKey(
                name: "FK_movimientos_banco_Facturas_FacturaId",
                schema: "banco",
                table: "movimientos_banco",
                column: "FacturaId",
                principalSchema: "contabilidad",
                principalTable: "Facturas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_movimientos_caja_Facturas_FacturaId",
                schema: "caja",
                table: "movimientos_caja",
                column: "FacturaId",
                principalSchema: "contabilidad",
                principalTable: "Facturas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_movimientos_banco_Facturas_FacturaId",
                schema: "banco",
                table: "movimientos_banco");

            migrationBuilder.DropForeignKey(
                name: "FK_movimientos_caja_Facturas_FacturaId",
                schema: "caja",
                table: "movimientos_caja");

            migrationBuilder.DropIndex(
                name: "IX_movimientos_caja_FacturaId",
                schema: "caja",
                table: "movimientos_caja");

            migrationBuilder.DropIndex(
                name: "IX_movimientos_banco_FacturaId",
                schema: "banco",
                table: "movimientos_banco");
        }
    }
}
