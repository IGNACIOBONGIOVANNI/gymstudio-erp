using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AsistenciasDeSocios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AsistenciasSocio",
                schema: "contabilidad",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SocioId = table.Column<long>(type: "bigint", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    RegistradaEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegistradaPor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsistenciasSocio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AsistenciasSocio_Socios_SocioId",
                        column: x => x.SocioId,
                        principalSchema: "contabilidad",
                        principalTable: "Socios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_asistencias_fecha",
                schema: "contabilidad",
                table: "AsistenciasSocio",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "ux_asistencias_socio_fecha",
                schema: "contabilidad",
                table: "AsistenciasSocio",
                columns: new[] { "SocioId", "Fecha" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsistenciasSocio",
                schema: "contabilidad");
        }
    }
}
