using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ActualizacionModelosAFIPySocios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SocioId",
                schema: "contabilidad",
                table: "Facturas",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_SocioId",
                schema: "contabilidad",
                table: "Facturas",
                column: "SocioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Facturas_Socios_SocioId",
                schema: "contabilidad",
                table: "Facturas",
                column: "SocioId",
                principalSchema: "contabilidad",
                principalTable: "Socios",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Facturas_Socios_SocioId",
                schema: "contabilidad",
                table: "Facturas");

            migrationBuilder.DropIndex(
                name: "IX_Facturas_SocioId",
                schema: "contabilidad",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "SocioId",
                schema: "contabilidad",
                table: "Facturas");
        }
    }
}
