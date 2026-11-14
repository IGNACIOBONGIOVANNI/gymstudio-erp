using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <summary>
    /// Desde el 14/11/2026 las repeticiones son un número (12) o un rango de menor a mayor (8-10). Esta migración pasa al formato
    /// nuevo las que estaban guardadas con texto: el primer número queda en las repeticiones (1 si no había ninguno) y el texto
    /// original pasa a las indicaciones, con «seg» y «min» escritos completos, así no se pierde nada («30 seg» queda 30 con la
    /// indicación «30 segundos»). Un rango con espacios de más («8 - 10») solo se acomoda. Las que ya estaban bien no se tocan.
    /// </summary>
    public partial class RepeticionesNumericas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH evaluadas AS (
                    SELECT "Id",
                           btrim("Repeticiones") AS original,
                           regexp_replace(btrim("Repeticiones"), '\s*-\s*', '-', 'g') AS sin_espacios
                    FROM contabilidad."RutinaEjercicios"
                ),
                clasificadas AS (
                    SELECT *,
                           CASE WHEN sin_espacios ~ '^\d{1,3}(-\d{1,3})?$'
                                THEN split_part(sin_espacios, '-', 1)::int > 0
                                     AND (position('-' IN sin_espacios) = 0
                                          OR split_part(sin_espacios, '-', 2)::int > split_part(sin_espacios, '-', 1)::int)
                                ELSE false
                           END AS valida
                    FROM evaluadas
                )
                UPDATE contabilidad."RutinaEjercicios" r
                SET "Repeticiones" = CASE WHEN c.valida THEN c.sin_espacios
                                          ELSE coalesce(substring(c.original FROM '[1-9]\d{0,2}'), '1') END,
                    "Indicaciones" = CASE WHEN c.valida THEN r."Indicaciones"
                                          ELSE left(concat_ws('. ', nullif(btrim(r."Indicaciones"), ''),
                                                    regexp_replace(regexp_replace(c.original,
                                                        '\mseg(undos)?\M\.?', 'segundos', 'gi'),
                                                        '\mmin(utos)?\M\.?', 'minutos', 'gi')), 200) END
                FROM clasificadas c
                WHERE r."Id" = c."Id" AND (NOT c.valida OR c.sin_espacios <> r."Repeticiones");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se deshace: el texto original quedó en las indicaciones y el formato viejo ya no lo acepta el dominio.
        }
    }
}
