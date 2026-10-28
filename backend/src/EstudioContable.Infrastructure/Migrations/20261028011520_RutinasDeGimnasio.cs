using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RutinasDeGimnasio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ejercicios",
                schema: "contabilidad",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GrupoMuscular = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ejercicios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rutinas",
                schema: "contabilidad",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Objetivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Nivel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false),
                    ModificadaEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rutinas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AsignacionesRutina",
                schema: "contabilidad",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SocioId = table.Column<long>(type: "bigint", nullable: false),
                    RutinaId = table.Column<long>(type: "bigint", nullable: false),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFin = table.Column<DateOnly>(type: "date", nullable: true),
                    Observaciones = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsignacionesRutina", x => x.Id);
                    table.CheckConstraint("CK_AsignacionesRutina_Fechas", "\"FechaFin\" IS NULL OR \"FechaFin\" >= \"FechaInicio\"");
                    table.ForeignKey(
                        name: "FK_AsignacionesRutina_Rutinas_RutinaId",
                        column: x => x.RutinaId,
                        principalSchema: "contabilidad",
                        principalTable: "Rutinas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AsignacionesRutina_Socios_SocioId",
                        column: x => x.SocioId,
                        principalSchema: "contabilidad",
                        principalTable: "Socios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RutinaEjercicios",
                schema: "contabilidad",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RutinaId = table.Column<long>(type: "bigint", nullable: false),
                    EjercicioId = table.Column<long>(type: "bigint", nullable: false),
                    Dia = table.Column<int>(type: "integer", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Series = table.Column<int>(type: "integer", nullable: false),
                    Repeticiones = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DescansoSegundos = table.Column<int>(type: "integer", nullable: false),
                    Indicaciones = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RutinaEjercicios", x => x.Id);
                    table.CheckConstraint("CK_RutinaEjercicios_Descanso", "\"DescansoSegundos\" BETWEEN 0 AND 600");
                    table.CheckConstraint("CK_RutinaEjercicios_Dia", "\"Dia\" BETWEEN 1 AND 7");
                    table.CheckConstraint("CK_RutinaEjercicios_Series", "\"Series\" BETWEEN 1 AND 20");
                    table.ForeignKey(
                        name: "FK_RutinaEjercicios_Ejercicios_EjercicioId",
                        column: x => x.EjercicioId,
                        principalSchema: "contabilidad",
                        principalTable: "Ejercicios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RutinaEjercicios_Rutinas_RutinaId",
                        column: x => x.RutinaId,
                        principalSchema: "contabilidad",
                        principalTable: "Rutinas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesRutina_RutinaId",
                schema: "contabilidad",
                table: "AsignacionesRutina",
                column: "RutinaId");

            migrationBuilder.CreateIndex(
                name: "ux_asignaciones_rutina_activa_por_socio",
                schema: "contabilidad",
                table: "AsignacionesRutina",
                column: "SocioId",
                unique: true,
                filter: "\"Activa\"");

            migrationBuilder.CreateIndex(
                name: "IX_RutinaEjercicios_EjercicioId",
                schema: "contabilidad",
                table: "RutinaEjercicios",
                column: "EjercicioId");

            migrationBuilder.CreateIndex(
                name: "IX_RutinaEjercicios_RutinaId",
                schema: "contabilidad",
                table: "RutinaEjercicios",
                column: "RutinaId");

            // Reglas que EF no expresa: nombres únicos sin distinguir mayúsculas y auditoría
            // de las tablas que se editan a mano (la lista de ejercicios de una rutina se
            // audita a través de Rutinas.ModificadaEn).
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX ux_ejercicios_nombre ON contabilidad.""Ejercicios"" (lower(""Nombre""));
                CREATE UNIQUE INDEX ux_rutinas_nombre    ON contabilidad.""Rutinas""    (lower(""Nombre""));

                CREATE TRIGGER trg_auditar_ejercicios   AFTER INSERT OR UPDATE OR DELETE ON contabilidad.""Ejercicios""         FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();
                CREATE TRIGGER trg_auditar_rutinas      AFTER INSERT OR UPDATE OR DELETE ON contabilidad.""Rutinas""            FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();
                CREATE TRIGGER trg_auditar_asignaciones AFTER INSERT OR UPDATE OR DELETE ON contabilidad.""AsignacionesRutina"" FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsignacionesRutina",
                schema: "contabilidad");

            migrationBuilder.DropTable(
                name: "RutinaEjercicios",
                schema: "contabilidad");

            migrationBuilder.DropTable(
                name: "Ejercicios",
                schema: "contabilidad");

            migrationBuilder.DropTable(
                name: "Rutinas",
                schema: "contabilidad");
        }
    }
}
