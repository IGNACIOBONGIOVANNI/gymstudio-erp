using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <summary>
    /// La ficha del entrenador suma el valor de la hora (nulo para las fichas que ya estaban: todavía sin valor) y nace la tabla de horas
    /// cumplidas por entrenador y día, con el valor de la hora de ese momento y el disparador de auditoría de cada fila. Solo agrega: ningún dato
    /// existente se toca. Hacia atrás se quitan la columna y la tabla de horas (las horas cargadas se pierden; la ficha y el resto de sus datos siguen).
    /// </summary>
    public partial class HorasYValorDeEntrenador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ValorHora",
                schema: "contabilidad",
                table: "Entrenadores",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HorasDeEntrenador",
                schema: "contabilidad",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntrenadorId = table.Column<long>(type: "bigint", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Horas = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    ValorHora = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Nota = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HorasDeEntrenador", x => x.Id);
                    table.CheckConstraint("CK_HorasDeEntrenador_Horas", "\"Horas\" BETWEEN 0 AND 24 AND \"Horas\" * 4 = trunc(\"Horas\" * 4)");
                    table.CheckConstraint("CK_HorasDeEntrenador_ValorHora", "\"ValorHora\" BETWEEN 0 AND 999999999999");
                    table.ForeignKey(
                        name: "FK_HorasDeEntrenador_Entrenadores_EntrenadorId",
                        column: x => x.EntrenadorId,
                        principalSchema: "contabilidad",
                        principalTable: "Entrenadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Entrenadores_ValorHora",
                schema: "contabilidad",
                table: "Entrenadores",
                sql: "\"ValorHora\" IS NULL OR \"ValorHora\" BETWEEN 0 AND 999999999999");

            migrationBuilder.CreateIndex(
                name: "ux_horas_entrenador_fecha",
                schema: "contabilidad",
                table: "HorasDeEntrenador",
                columns: new[] { "EntrenadorId", "Fecha" },
                unique: true);

            // Auditoría de cada fila de horas (quién cargó o cambió qué día). EF no sabe describir un disparador: va con SQL.
            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_auditar_horas_entrenador AFTER INSERT OR UPDATE OR DELETE ON contabilidad.""HorasDeEntrenador"" FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_auditar_horas_entrenador ON contabilidad.""HorasDeEntrenador"";");

            migrationBuilder.DropTable(
                name: "HorasDeEntrenador",
                schema: "contabilidad");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Entrenadores_ValorHora",
                schema: "contabilidad",
                table: "Entrenadores");

            migrationBuilder.DropColumn(
                name: "ValorHora",
                schema: "contabilidad",
                table: "Entrenadores");
        }
    }
}
