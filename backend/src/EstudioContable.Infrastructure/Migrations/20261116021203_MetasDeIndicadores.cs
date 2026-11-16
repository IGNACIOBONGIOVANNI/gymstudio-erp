using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MetasDeIndicadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetasDeIndicadores",
                schema: "contabilidad",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ActualizadoPor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetasDeIndicadores", x => x.Clave);
                    table.CheckConstraint("CK_MetasDeIndicadores_Valor", "\"Valor\" >= 0");
                });

            // Cada cambio de una meta queda en la auditoría, como el resto de las tablas.
            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_auditar_metas_indicadores AFTER INSERT OR UPDATE OR DELETE ON contabilidad.""MetasDeIndicadores"" FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_auditar_metas_indicadores ON contabilidad.""MetasDeIndicadores"";");
            migrationBuilder.DropTable(
                name: "MetasDeIndicadores",
                schema: "contabilidad");
        }
    }
}
