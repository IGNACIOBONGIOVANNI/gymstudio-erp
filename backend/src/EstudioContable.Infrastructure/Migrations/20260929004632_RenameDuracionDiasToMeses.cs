using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameDuracionDiasToMeses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DuracionDias",
                schema: "contabilidad",
                table: "Planes",
                newName: "DuracionMeses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DuracionMeses",
                schema: "contabilidad",
                table: "Planes",
                newName: "DuracionDias");
        }
    }
}
