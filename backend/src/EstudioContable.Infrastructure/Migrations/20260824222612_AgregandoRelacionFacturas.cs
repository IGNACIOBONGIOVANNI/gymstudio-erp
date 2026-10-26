using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregandoRelacionFacturas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FacturaId",
                schema: "caja",
                table: "movimientos_caja",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FacturaId",
                schema: "banco",
                table: "movimientos_banco",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FacturaId",
                schema: "caja",
                table: "movimientos_caja");

            migrationBuilder.DropColumn(
                name: "FacturaId",
                schema: "banco",
                table: "movimientos_banco");
        }
    }
}
