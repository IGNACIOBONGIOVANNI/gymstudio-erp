using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <summary>
    /// El calendario deja de guardar el nombre de quien está a cargo como texto y pasa a apuntar a la ficha de un entrenador.
    /// Los nombres que ya estaban cargados se convierten en fichas (una por persona, sin distinguir mayúsculas ni espacios
    /// sobrantes), así el calendario queda igual. Hacia atrás vuelve el texto con el nombre de la ficha.
    /// El orden importa: primero se crean las fichas y se enlazan los turnos, y recién después se quita la columna de texto.
    /// </summary>
    public partial class EntrenadoresEnElCalendario : Migration
    {
        // Nombre limpio, como lo guarda Entrenador.LimpiarNombre: cualquier racha de espacios (también el duro y otros espacios Unicode)
        // pasa a un solo espacio y se recortan los extremos.
        private static string NombreLimpio(string columna) => $@"btrim(regexp_replace({columna}, '[\s\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]+', ' ', 'g'))";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Entrenadores",
                schema: "contabilidad",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NombreCompleto = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Telefono = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Especialidad = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCreacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entrenadores", x => x.Id);
                    table.CheckConstraint("CK_Entrenadores_Nombre", "char_length(btrim(\"NombreCompleto\")) > 0");
                });

            // El nombre no se repite sin importar las mayúsculas. EF no sabe modelar un índice sobre una expresión: va con SQL.
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX ux_entrenadores_nombre ON contabilidad.""Entrenadores"" (lower(""NombreCompleto""));");

            // Una ficha por persona, con la primera grafía que aparece (por día y hora).
            var limpioSolo = NombreLimpio(@"""Encargado""");
            var limpioDeT = NombreLimpio(@"t.""Encargado""");
            migrationBuilder.Sql($@"
                INSERT INTO contabilidad.""Entrenadores"" (""NombreCompleto"", ""Activo"", ""FechaCreacion"")
                SELECT DISTINCT ON (lower(n)) n, true, now()
                FROM (SELECT {limpioSolo} AS n, ""DiaSemana"", ""Hora"" FROM contabilidad.""TurnosEncargado"") t
                ORDER BY lower(n), ""DiaSemana"", ""Hora"";
            ");

            migrationBuilder.AddColumn<long>(
                name: "EntrenadorId",
                schema: "contabilidad",
                table: "TurnosEncargado",
                type: "bigint",
                nullable: true);

            // Este UPDATE dispara el trigger de auditoría que TurnosEncargado ya tiene: queda un registro por turno migrado.
            migrationBuilder.Sql($@"
                UPDATE contabilidad.""TurnosEncargado"" t SET ""EntrenadorId"" = e.""Id""
                FROM contabilidad.""Entrenadores"" e
                WHERE lower(e.""NombreCompleto"") = lower({limpioDeT});
            ");

            migrationBuilder.AlterColumn<long>(
                name: "EntrenadorId",
                schema: "contabilidad",
                table: "TurnosEncargado",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TurnosEncargado_EntrenadorId",
                schema: "contabilidad",
                table: "TurnosEncargado",
                column: "EntrenadorId");

            migrationBuilder.AddForeignKey(
                name: "FK_TurnosEncargado_Entrenadores_EntrenadorId",
                schema: "contabilidad",
                table: "TurnosEncargado",
                column: "EntrenadorId",
                principalSchema: "contabilidad",
                principalTable: "Entrenadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropCheckConstraint(
                name: "CK_TurnosEncargado_Nombre",
                schema: "contabilidad",
                table: "TurnosEncargado");

            migrationBuilder.DropColumn(
                name: "Encargado",
                schema: "contabilidad",
                table: "TurnosEncargado");

            // Auditoría de las fichas (después de la carga inicial, para no auditar la conversión en masa).
            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_auditar_entrenadores AFTER INSERT OR UPDATE OR DELETE ON contabilidad.""Entrenadores"" FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Encargado",
                schema: "contabilidad",
                table: "TurnosEncargado",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE contabilidad.""TurnosEncargado"" t SET ""Encargado"" = e.""NombreCompleto""
                FROM contabilidad.""Entrenadores"" e
                WHERE e.""Id"" = t.""EntrenadorId"";
            ");

            migrationBuilder.AlterColumn<string>(
                name: "Encargado",
                schema: "contabilidad",
                table: "TurnosEncargado",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TurnosEncargado_Nombre",
                schema: "contabilidad",
                table: "TurnosEncargado",
                sql: "char_length(btrim(\"Encargado\")) > 0");

            migrationBuilder.DropForeignKey(
                name: "FK_TurnosEncargado_Entrenadores_EntrenadorId",
                schema: "contabilidad",
                table: "TurnosEncargado");

            migrationBuilder.DropIndex(
                name: "IX_TurnosEncargado_EntrenadorId",
                schema: "contabilidad",
                table: "TurnosEncargado");

            migrationBuilder.DropColumn(
                name: "EntrenadorId",
                schema: "contabilidad",
                table: "TurnosEncargado");

            // Al borrar la tabla se van con ella su índice único y su trigger de auditoría.
            migrationBuilder.DropTable(
                name: "Entrenadores",
                schema: "contabilidad");
        }
    }
}
