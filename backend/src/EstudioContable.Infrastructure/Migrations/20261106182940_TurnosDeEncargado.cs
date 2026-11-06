using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TurnosDeEncargado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TurnosEncargado",
                schema: "contabilidad",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiaSemana = table.Column<int>(type: "integer", nullable: false),
                    Hora = table.Column<int>(type: "integer", nullable: false),
                    Encargado = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnosEncargado", x => x.Id);
                    table.CheckConstraint("CK_TurnosEncargado_Dia", "\"DiaSemana\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_TurnosEncargado_Hora", "\"Hora\" BETWEEN 6 AND 21");
                    table.CheckConstraint("CK_TurnosEncargado_Nombre", "char_length(btrim(\"Encargado\")) > 0");
                });

            migrationBuilder.CreateIndex(
                name: "ux_turnos_encargado_dia_hora",
                schema: "contabilidad",
                table: "TurnosEncargado",
                columns: new[] { "DiaSemana", "Hora" },
                unique: true);

            // Auditoría del calendario: quién cambió quién está de encargado (la tabla de auditoría ya existe).
            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_auditar_turnos_encargado AFTER INSERT OR UPDATE OR DELETE ON contabilidad.""TurnosEncargado"" FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TurnosEncargado",
                schema: "contabilidad");
        }
    }
}
