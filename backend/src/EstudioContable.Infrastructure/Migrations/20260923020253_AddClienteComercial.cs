using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClienteComercial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ClienteComercialId",
                schema: "contabilidad",
                table: "Facturas",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClientesComerciales",
                schema: "contabilidad",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RazonSocial = table.Column<string>(type: "text", nullable: false),
                    Direccion = table.Column<string>(type: "text", nullable: false),
                    Localidad = table.Column<string>(type: "text", nullable: false),
                    Cuit = table.Column<string>(type: "text", nullable: false),
                    CondicionIva = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientesComerciales", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_ClienteComercialId",
                schema: "contabilidad",
                table: "Facturas",
                column: "ClienteComercialId");

            migrationBuilder.AddForeignKey(
                name: "FK_Facturas_ClientesComerciales_ClienteComercialId",
                schema: "contabilidad",
                table: "Facturas",
                column: "ClienteComercialId",
                principalSchema: "contabilidad",
                principalTable: "ClientesComerciales",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Facturas_ClientesComerciales_ClienteComercialId",
                schema: "contabilidad",
                table: "Facturas");

            migrationBuilder.DropTable(
                name: "ClientesComerciales",
                schema: "contabilidad");

            migrationBuilder.DropIndex(
                name: "IX_Facturas_ClienteComercialId",
                schema: "contabilidad",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "ClienteComercialId",
                schema: "contabilidad",
                table: "Facturas");
        }
    }
}
