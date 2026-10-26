using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CorregirFkPlanActualSocio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Socios_Planes_PlanActualObjId",
                schema: "contabilidad",
                table: "Socios");

            migrationBuilder.DropIndex(
                name: "IX_Socios_PlanActualObjId",
                schema: "contabilidad",
                table: "Socios");

            migrationBuilder.DropColumn(
                name: "PlanActualObjId",
                schema: "contabilidad",
                table: "Socios");

            // Hasta ahora no había FK: un socio podía quedar apuntando a un plan borrado.
            // Se limpian esas referencias huérfanas para que la FK nueva pueda crearse.
            migrationBuilder.Sql(
                @"UPDATE contabilidad.""Socios"" s
                  SET ""PlanActualId"" = NULL
                  WHERE s.""PlanActualId"" IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM contabilidad.""Planes"" p WHERE p.""Id"" = s.""PlanActualId"");");

            migrationBuilder.CreateIndex(
                name: "IX_Socios_PlanActualId",
                schema: "contabilidad",
                table: "Socios",
                column: "PlanActualId");

            migrationBuilder.AddForeignKey(
                name: "FK_Socios_Planes_PlanActualId",
                schema: "contabilidad",
                table: "Socios",
                column: "PlanActualId",
                principalSchema: "contabilidad",
                principalTable: "Planes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Socios_Planes_PlanActualId",
                schema: "contabilidad",
                table: "Socios");

            migrationBuilder.DropIndex(
                name: "IX_Socios_PlanActualId",
                schema: "contabilidad",
                table: "Socios");

            migrationBuilder.AddColumn<long>(
                name: "PlanActualObjId",
                schema: "contabilidad",
                table: "Socios",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Socios_PlanActualObjId",
                schema: "contabilidad",
                table: "Socios",
                column: "PlanActualObjId");

            migrationBuilder.AddForeignKey(
                name: "FK_Socios_Planes_PlanActualObjId",
                schema: "contabilidad",
                table: "Socios",
                column: "PlanActualObjId",
                principalSchema: "contabilidad",
                principalTable: "Planes",
                principalColumn: "Id");
        }
    }
}
