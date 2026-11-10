using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SociosDniUnico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Si ya hay socios con el DNI repetido (se podían crear con dos altas a la vez), el índice no se puede armar: se avisa
            // cuáles son en vez de fallar con el mensaje técnico. No se borra ni se cambia ningún socio.
            migrationBuilder.Sql("""
                DO $$
                DECLARE repetidos text;
                BEGIN
                    SELECT string_agg("Dni", ', ') INTO repetidos
                    FROM (SELECT "Dni" FROM contabilidad."Socios" GROUP BY "Dni" HAVING COUNT(*) > 1 ORDER BY "Dni" LIMIT 20) d;
                    IF repetidos IS NOT NULL THEN
                        RAISE EXCEPTION 'Hay socios con el DNI repetido (%). Corregí o eliminá los repetidos y volvé a iniciar el sistema.', repetidos;
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_socios_dni",
                schema: "contabilidad",
                table: "Socios",
                column: "Dni",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_socios_dni",
                schema: "contabilidad",
                table: "Socios");
        }
    }
}
