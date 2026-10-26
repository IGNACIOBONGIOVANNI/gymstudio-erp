using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarFacturasVenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ventas");

            migrationBuilder.CreateTable(
                name: "facturas_venta",
                schema: "ventas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cliente_id = table.Column<long>(type: "bigint", nullable: false),
                    fecha = table.Column<DateTime>(type: "date", nullable: false),
                    tipo_factura = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    numero_factura = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cliente_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cuit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    neto = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    iva = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_facturas_venta", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "ventas",
                table: "facturas_venta",
                columns: new[] { "Id", "cliente_id", "cliente_nombre", "cuit", "fecha", "iva", "neto", "numero_factura", "tipo_factura", "total" },
                values: new object[,]
                {
                    { 1L, 1L, "Destornilladores Juego", "XX", new DateTime(2026, 5, 15, 0, 0, 0, 0, DateTimeKind.Utc), 8820m, 42000m, "0001-00", "A", 50820m },
                    { 2L, 1L, "Buscapolo", "XX", new DateTime(2026, 5, 15, 0, 0, 0, 0, DateTimeKind.Utc), 12600m, 60000m, "0002-00", "A", 72600m },
                    { 3L, 1L, "soldadora mig", "XX", new DateTime(2026, 5, 15, 0, 0, 0, 0, DateTimeKind.Utc), 285600m, 1360000m, "0003-00", "A", 1645600m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "facturas_venta",
                schema: "ventas");
        }
    }
}
