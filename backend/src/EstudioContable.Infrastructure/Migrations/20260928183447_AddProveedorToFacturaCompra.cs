using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProveedorToFacturaCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ProveedorId",
                schema: "compras",
                table: "facturas_compra",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_facturas_compra_ProveedorId",
                schema: "compras",
                table: "facturas_compra",
                column: "ProveedorId");

            migrationBuilder.AddForeignKey(
                name: "FK_facturas_compra_Proveedores_ProveedorId",
                schema: "compras",
                table: "facturas_compra",
                column: "ProveedorId",
                principalSchema: "contabilidad",
                principalTable: "Proveedores",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_facturas_compra_Proveedores_ProveedorId",
                schema: "compras",
                table: "facturas_compra");

            migrationBuilder.DropIndex(
                name: "IX_facturas_compra_ProveedorId",
                schema: "compras",
                table: "facturas_compra");

            migrationBuilder.DropColumn(
                name: "ProveedorId",
                schema: "compras",
                table: "facturas_compra");
        }
    }
}
