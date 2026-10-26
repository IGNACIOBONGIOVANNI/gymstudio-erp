using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCamposNutricionalesProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Calorias",
                schema: "inventario",
                table: "Productos",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Carbohidratos",
                schema: "inventario",
                table: "Productos",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Grasas",
                schema: "inventario",
                table: "Productos",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Marca",
                schema: "inventario",
                table: "Productos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Proteinas",
                schema: "inventario",
                table: "Productos",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sabor",
                schema: "inventario",
                table: "Productos",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "inventario",
                table: "Productos",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "Calorias", "Carbohidratos", "Grasas", "Marca", "Proteinas", "Sabor" },
                values: new object[] { null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                schema: "inventario",
                table: "Productos",
                keyColumn: "Id",
                keyValue: 2L,
                columns: new[] { "Calorias", "Carbohidratos", "Grasas", "Marca", "Proteinas", "Sabor" },
                values: new object[] { null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                schema: "inventario",
                table: "Productos",
                keyColumn: "Id",
                keyValue: 3L,
                columns: new[] { "Calorias", "Carbohidratos", "Grasas", "Marca", "Proteinas", "Sabor" },
                values: new object[] { null, null, null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Calorias",
                schema: "inventario",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "Carbohidratos",
                schema: "inventario",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "Grasas",
                schema: "inventario",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "Marca",
                schema: "inventario",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "Proteinas",
                schema: "inventario",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "Sabor",
                schema: "inventario",
                table: "Productos");
        }
    }
}
