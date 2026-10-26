using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarFacturasCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "compras");

            migrationBuilder.CreateTable(
                name: "facturas_compra",
                schema: "compras",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cliente_id = table.Column<long>(type: "bigint", nullable: false),
                    fecha = table.Column<DateTime>(type: "date", nullable: false),
                    tipo_factura = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    numero_factura = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    proveedor_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cuit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    neto = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    iva = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_facturas_compra", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "compras",
                table: "facturas_compra",
                columns: new[] { "Id", "cliente_id", "cuit", "fecha", "iva", "neto", "numero_factura", "proveedor_nombre", "tipo_factura", "total" },
                values: new object[,]
                {
                    { 1L, 1L, "XX", new DateTime(2026, 6, 5, 0, 0, 0, 0, DateTimeKind.Utc), 2100m, 10000m, "0001-00", "Tornillo", "A", 12100m },
                    { 2L, 1L, "XX", new DateTime(2026, 6, 5, 0, 0, 0, 0, DateTimeKind.Utc), 4200m, 20000m, "0002-00", "Tuerca", "A", 24200m },
                    { 3L, 1L, "XX", new DateTime(2026, 6, 5, 0, 0, 0, 0, DateTimeKind.Utc), 840m, 4000m, "0003-00", "Arandelas", "A", 4840m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "facturas_compra",
                schema: "compras");
        }
    }
}
