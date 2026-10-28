using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CuentasCorrientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ClienteComercialId",
                schema: "caja",
                table: "movimientos_caja",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProveedorId",
                schema: "caja",
                table: "movimientos_caja",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ClienteComercialId",
                schema: "banco",
                table: "movimientos_banco",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProveedorId",
                schema: "banco",
                table: "movimientos_banco",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_caja_ClienteComercialId",
                schema: "caja",
                table: "movimientos_caja",
                column: "ClienteComercialId");

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_caja_ProveedorId",
                schema: "caja",
                table: "movimientos_caja",
                column: "ProveedorId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_movimientos_caja_un_solo_tercero",
                schema: "caja",
                table: "movimientos_caja",
                sql: "NOT (\"ClienteComercialId\" IS NOT NULL AND \"ProveedorId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_banco_ClienteComercialId",
                schema: "banco",
                table: "movimientos_banco",
                column: "ClienteComercialId");

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_banco_ProveedorId",
                schema: "banco",
                table: "movimientos_banco",
                column: "ProveedorId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_movimientos_banco_un_solo_tercero",
                schema: "banco",
                table: "movimientos_banco",
                sql: "NOT (\"ClienteComercialId\" IS NOT NULL AND \"ProveedorId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_movimientos_banco_ClientesComerciales_ClienteComercialId",
                schema: "banco",
                table: "movimientos_banco",
                column: "ClienteComercialId",
                principalSchema: "contabilidad",
                principalTable: "ClientesComerciales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_movimientos_banco_Proveedores_ProveedorId",
                schema: "banco",
                table: "movimientos_banco",
                column: "ProveedorId",
                principalSchema: "contabilidad",
                principalTable: "Proveedores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_movimientos_caja_ClientesComerciales_ClienteComercialId",
                schema: "caja",
                table: "movimientos_caja",
                column: "ClienteComercialId",
                principalSchema: "contabilidad",
                principalTable: "ClientesComerciales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_movimientos_caja_Proveedores_ProveedorId",
                schema: "caja",
                table: "movimientos_caja",
                column: "ProveedorId",
                principalSchema: "contabilidad",
                principalTable: "Proveedores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_movimientos_banco_ClientesComerciales_ClienteComercialId",
                schema: "banco",
                table: "movimientos_banco");

            migrationBuilder.DropForeignKey(
                name: "FK_movimientos_banco_Proveedores_ProveedorId",
                schema: "banco",
                table: "movimientos_banco");

            migrationBuilder.DropForeignKey(
                name: "FK_movimientos_caja_ClientesComerciales_ClienteComercialId",
                schema: "caja",
                table: "movimientos_caja");

            migrationBuilder.DropForeignKey(
                name: "FK_movimientos_caja_Proveedores_ProveedorId",
                schema: "caja",
                table: "movimientos_caja");

            migrationBuilder.DropIndex(
                name: "IX_movimientos_caja_ClienteComercialId",
                schema: "caja",
                table: "movimientos_caja");

            migrationBuilder.DropIndex(
                name: "IX_movimientos_caja_ProveedorId",
                schema: "caja",
                table: "movimientos_caja");

            migrationBuilder.DropCheckConstraint(
                name: "CK_movimientos_caja_un_solo_tercero",
                schema: "caja",
                table: "movimientos_caja");

            migrationBuilder.DropIndex(
                name: "IX_movimientos_banco_ClienteComercialId",
                schema: "banco",
                table: "movimientos_banco");

            migrationBuilder.DropIndex(
                name: "IX_movimientos_banco_ProveedorId",
                schema: "banco",
                table: "movimientos_banco");

            migrationBuilder.DropCheckConstraint(
                name: "CK_movimientos_banco_un_solo_tercero",
                schema: "banco",
                table: "movimientos_banco");

            migrationBuilder.DropColumn(
                name: "ClienteComercialId",
                schema: "caja",
                table: "movimientos_caja");

            migrationBuilder.DropColumn(
                name: "ProveedorId",
                schema: "caja",
                table: "movimientos_caja");

            migrationBuilder.DropColumn(
                name: "ClienteComercialId",
                schema: "banco",
                table: "movimientos_banco");

            migrationBuilder.DropColumn(
                name: "ProveedorId",
                schema: "banco",
                table: "movimientos_banco");
        }
    }
}
