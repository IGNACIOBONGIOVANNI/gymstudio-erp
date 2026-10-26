using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSeedDataV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cliente",
                schema: "contabilidad",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RazonSocial = table.Column<string>(type: "text", nullable: false),
                    NombreFantasia = table.Column<string>(type: "text", nullable: true),
                    Cuit = table.Column<string>(type: "text", nullable: false),
                    TipoContribuyente = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Telefono = table.Column<string>(type: "text", nullable: true),
                    DomicilioFiscal = table.Column<string>(type: "text", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    FechaAlta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cliente", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuario",
                schema: "contabilidad",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NombreCompleto = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Rol = table.Column<int>(type: "integer", nullable: false),
                    ClienteId = table.Column<long>(type: "bigint", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    UltimoLogin = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FechaCreacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usuario_Cliente_ClienteId",
                        column: x => x.ClienteId,
                        principalSchema: "contabilidad",
                        principalTable: "Cliente",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                schema: "contabilidad",
                table: "Cliente",
                columns: new[] { "Id", "Activo", "Cuit", "DomicilioFiscal", "Email", "FechaAlta", "NombreFantasia", "RazonSocial", "Telefono", "TipoContribuyente" },
                values: new object[] { 1L, true, "20111111112", null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Cliente Demo", null, 1 });

            migrationBuilder.InsertData(
                schema: "contabilidad",
                table: "CuentasContables",
                columns: new[] { "Id", "Activa", "ClienteId", "Codigo", "CuentaPadreId", "Naturaleza", "Nombre" },
                values: new object[,]
                {
                    { 101L, true, 1L, "1.1.1.01", null, 0, "Caja" },
                    { 104L, true, 1L, "4.1.1.01", null, 3, "Ventas" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Usuario_ClienteId",
                schema: "contabilidad",
                table: "Usuario",
                column: "ClienteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Usuario",
                schema: "contabilidad");

            migrationBuilder.DropTable(
                name: "Cliente",
                schema: "contabilidad");

            migrationBuilder.DeleteData(
                schema: "contabilidad",
                table: "CuentasContables",
                keyColumn: "Id",
                keyValue: 101L);

            migrationBuilder.DeleteData(
                schema: "contabilidad",
                table: "CuentasContables",
                keyColumn: "Id",
                keyValue: 104L);
        }
    }
}
