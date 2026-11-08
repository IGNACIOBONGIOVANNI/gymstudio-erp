using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CopiasAutomaticas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracionDeCopias",
                schema: "contabilidad",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false),
                    Frecuencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Hora = table.Column<int>(type: "integer", nullable: false),
                    Minuto = table.Column<int>(type: "integer", nullable: false),
                    DiaDeLaSemana = table.Column<int>(type: "integer", nullable: true),
                    DiaDelMes = table.Column<int>(type: "integer", nullable: true),
                    Conservar = table.Column<int>(type: "integer", nullable: false),
                    ActivaDesde = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UltimaCopia = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UltimoIntento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UltimoError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionDeCopias", x => x.Id);
                    table.CheckConstraint("CK_ConfiguracionDeCopias_Conservar", "\"Conservar\" BETWEEN 1 AND 60");
                    table.CheckConstraint("CK_ConfiguracionDeCopias_DiaMes", "\"DiaDelMes\" IS NULL OR \"DiaDelMes\" BETWEEN 1 AND 28");
                    table.CheckConstraint("CK_ConfiguracionDeCopias_DiaSemana", "\"DiaDeLaSemana\" IS NULL OR \"DiaDeLaSemana\" BETWEEN 1 AND 7");
                    table.CheckConstraint("CK_ConfiguracionDeCopias_Frecuencia", "\"Frecuencia\" IN ('CadaSeisHoras', 'CadaDoceHoras', 'Diaria', 'Semanal', 'Mensual')");
                    table.CheckConstraint("CK_ConfiguracionDeCopias_Hora", "\"Hora\" BETWEEN 0 AND 23");
                    table.CheckConstraint("CK_ConfiguracionDeCopias_Minuto", "\"Minuto\" BETWEEN 0 AND 59");
                    table.CheckConstraint("CK_ConfiguracionDeCopias_Unica", "\"Id\" = 1");
                });

            // La única fila, ya creada: apagada, una copia por día a las 22:00 y las últimas 14 guardadas.
            migrationBuilder.Sql(@"
                INSERT INTO contabilidad.""ConfiguracionDeCopias"" (""Id"", ""Activa"", ""Frecuencia"", ""Hora"", ""Minuto"", ""Conservar"", ""ActivaDesde"")
                VALUES (1, false, 'Diaria', 22, 0, 14, now());
            ");

            // Auditoría de la programación (después de la fila inicial, para no auditarla).
            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_auditar_configuracion_copias AFTER INSERT OR UPDATE OR DELETE ON contabilidad.""ConfiguracionDeCopias"" FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionDeCopias",
                schema: "contabilidad");
        }
    }
}
