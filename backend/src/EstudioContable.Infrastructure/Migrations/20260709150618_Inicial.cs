using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "contabilidad");

            migrationBuilder.CreateTable(
                name: "asientos_contables",
                schema: "contabilidad",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cliente_id = table.Column<long>(type: "bigint", nullable: false),
                    numero_asiento = table.Column<long>(type: "bigint", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    usuario_creador_id = table.Column<long>(type: "bigint", nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_confirmacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    usuario_confirmacion_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asientos_contables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CuentasContables",
                schema: "contabilidad",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClienteId = table.Column<long>(type: "bigint", nullable: false),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Naturaleza = table.Column<int>(type: "integer", nullable: false),
                    CuentaPadreId = table.Column<long>(type: "bigint", nullable: true),
                    Activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuentasContables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "lineas_asiento",
                schema: "contabilidad",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    asiento_id = table.Column<long>(type: "bigint", nullable: false),
                    cuenta_id = table.Column<long>(type: "bigint", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    debe = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    haber = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    orden = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lineas_asiento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lineas_asiento_CuentasContables_cuenta_id",
                        column: x => x.cuenta_id,
                        principalSchema: "contabilidad",
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lineas_asiento_asientos_contables_asiento_id",
                        column: x => x.asiento_id,
                        principalSchema: "contabilidad",
                        principalTable: "asientos_contables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_asientos_contables_cliente_id_fecha",
                schema: "contabilidad",
                table: "asientos_contables",
                columns: new[] { "cliente_id", "fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_asientos_contables_cliente_id_numero_asiento",
                schema: "contabilidad",
                table: "asientos_contables",
                columns: new[] { "cliente_id", "numero_asiento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lineas_asiento_asiento_id",
                schema: "contabilidad",
                table: "lineas_asiento",
                column: "asiento_id");

            migrationBuilder.CreateIndex(
                name: "IX_lineas_asiento_cuenta_id",
                schema: "contabilidad",
                table: "lineas_asiento",
                column: "cuenta_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lineas_asiento",
                schema: "contabilidad");

            migrationBuilder.DropTable(
                name: "CuentasContables",
                schema: "contabilidad");

            migrationBuilder.DropTable(
                name: "asientos_contables",
                schema: "contabilidad");
        }
    }
}
