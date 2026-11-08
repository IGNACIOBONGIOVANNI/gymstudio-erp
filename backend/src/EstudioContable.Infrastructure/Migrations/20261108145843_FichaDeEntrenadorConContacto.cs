using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <summary>
    /// La ficha del entrenador suma un email y un WhatsApp (nulos para las fichas que ya estaban) y su foto, que vive en una tabla
    /// aparte sin disparador de auditoría. Solo agrega: ningún dato existente se toca. Hacia atrás se quitan las columnas y la tabla
    /// de fotos (las fotos cargadas se pierden; la ficha y el resto de sus datos siguen).
    /// </summary>
    public partial class FichaDeEntrenadorConContacto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "contabilidad",
                table: "Entrenadores",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsApp",
                schema: "contabilidad",
                table: "Entrenadores",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FotosDeEntrenador",
                schema: "contabilidad",
                columns: table => new
                {
                    EntrenadorId = table.Column<long>(type: "bigint", nullable: false),
                    Foto = table.Column<byte[]>(type: "bytea", nullable: false),
                    Actualizada = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FotosDeEntrenador", x => x.EntrenadorId);
                    table.CheckConstraint("CK_FotosDeEntrenador_Tamano", "octet_length(\"Foto\") BETWEEN 100 AND 150000");
                    table.ForeignKey(
                        name: "FK_FotosDeEntrenador_Entrenadores_EntrenadorId",
                        column: x => x.EntrenadorId,
                        principalSchema: "contabilidad",
                        principalTable: "Entrenadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Entrenadores_Email",
                schema: "contabilidad",
                table: "Entrenadores",
                sql: "\"Email\" IS NULL OR \"Email\" ~ '^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FotosDeEntrenador",
                schema: "contabilidad");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Entrenadores_Email",
                schema: "contabilidad",
                table: "Entrenadores");

            migrationBuilder.DropColumn(
                name: "Email",
                schema: "contabilidad",
                table: "Entrenadores");

            migrationBuilder.DropColumn(
                name: "WhatsApp",
                schema: "contabilidad",
                table: "Entrenadores");
        }
    }
}
