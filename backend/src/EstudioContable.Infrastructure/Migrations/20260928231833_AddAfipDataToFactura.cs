using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAfipDataToFactura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cae",
                schema: "contabilidad",
                table: "Facturas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaVencimientoCae",
                schema: "contabilidad",
                table: "Facturas",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cae",
                schema: "contabilidad",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "FechaVencimientoCae",
                schema: "contabilidad",
                table: "Facturas");
        }
    }
}
