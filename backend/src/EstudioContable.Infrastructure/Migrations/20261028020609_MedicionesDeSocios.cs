using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MedicionesDeSocios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MedicionesSocio",
                schema: "contabilidad",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SocioId = table.Column<long>(type: "bigint", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    PesoKg = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: false),
                    GrasaCorporalPct = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    CinturaCm = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    CaderaCm = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    PechoCm = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    BrazoCm = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    MusloCm = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    Notas = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicionesSocio", x => x.Id);
                    table.CheckConstraint("CK_MedicionesSocio_Grasa", "\"GrasaCorporalPct\" IS NULL OR \"GrasaCorporalPct\" BETWEEN 2 AND 70");
                    table.CheckConstraint("CK_MedicionesSocio_Perimetros", "(\"CinturaCm\" IS NULL OR \"CinturaCm\" BETWEEN 30 AND 250) AND (\"CaderaCm\" IS NULL OR \"CaderaCm\" BETWEEN 30 AND 250) AND (\"PechoCm\" IS NULL OR \"PechoCm\" BETWEEN 30 AND 250) AND (\"BrazoCm\" IS NULL OR \"BrazoCm\" BETWEEN 10 AND 100) AND (\"MusloCm\" IS NULL OR \"MusloCm\" BETWEEN 20 AND 150)");
                    table.CheckConstraint("CK_MedicionesSocio_Peso", "\"PesoKg\" BETWEEN 20 AND 400");
                    table.ForeignKey(
                        name: "FK_MedicionesSocio_Socios_SocioId",
                        column: x => x.SocioId,
                        principalSchema: "contabilidad",
                        principalTable: "Socios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_mediciones_socio_fecha",
                schema: "contabilidad",
                table: "MedicionesSocio",
                columns: new[] { "SocioId", "Fecha" },
                unique: true);

            // Auditoría de las mediciones (la tabla de sólo lectura de auditoría ya existe).
            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_auditar_mediciones AFTER INSERT OR UPDATE OR DELETE ON contabilidad.""MedicionesSocio"" FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MedicionesSocio",
                schema: "contabilidad");
        }
    }
}
