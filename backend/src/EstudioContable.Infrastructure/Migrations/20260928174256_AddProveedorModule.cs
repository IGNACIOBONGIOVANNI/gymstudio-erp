using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProveedorModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ProveedorId",
                schema: "contabilidad",
                table: "Facturas",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Proveedores",
                schema: "contabilidad",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RazonSocial = table.Column<string>(type: "text", nullable: false),
                    Direccion = table.Column<string>(type: "text", nullable: false),
                    Localidad = table.Column<string>(type: "text", nullable: false),
                    Cuit = table.Column<string>(type: "text", nullable: false),
                    CondicionIva = table.Column<string>(type: "text", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proveedores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_ProveedorId",
                schema: "contabilidad",
                table: "Facturas",
                column: "ProveedorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Facturas_Proveedores_ProveedorId",
                schema: "contabilidad",
                table: "Facturas",
                column: "ProveedorId",
                principalSchema: "contabilidad",
                principalTable: "Proveedores",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Facturas_Proveedores_ProveedorId",
                schema: "contabilidad",
                table: "Facturas");

            migrationBuilder.DropTable(
                name: "Proveedores",
                schema: "contabilidad");

            migrationBuilder.DropIndex(
                name: "IX_Facturas_ProveedorId",
                schema: "contabilidad",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "ProveedorId",
                schema: "contabilidad",
                table: "Facturas");
        }
    }
}
