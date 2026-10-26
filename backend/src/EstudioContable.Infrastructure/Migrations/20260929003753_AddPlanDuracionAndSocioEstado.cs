using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanDuracionAndSocioEstado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Estado",
                schema: "contabilidad",
                table: "Socios",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaVencimiento",
                schema: "contabilidad",
                table: "Socios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PlanActualId",
                schema: "contabilidad",
                table: "Socios",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PlanActualObjId",
                schema: "contabilidad",
                table: "Socios",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DuracionDias",
                schema: "contabilidad",
                table: "Planes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
                name: "Estado",
                schema: "contabilidad",
                table: "Socios");

            migrationBuilder.DropColumn(
                name: "FechaVencimiento",
                schema: "contabilidad",
                table: "Socios");

            migrationBuilder.DropColumn(
                name: "PlanActualId",
                schema: "contabilidad",
                table: "Socios");

            migrationBuilder.DropColumn(
                name: "PlanActualObjId",
                schema: "contabilidad",
                table: "Socios");

            migrationBuilder.DropColumn(
                name: "DuracionDias",
                schema: "contabilidad",
                table: "Planes");
        }
    }
}
