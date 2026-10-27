using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AnularContraasientos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Antes, anular o editar generaba un contraasiento (Debe y Haber invertidos), y
            // así aparecían importes del lado contrario en cuentas como el IVA Crédito
            // Fiscal. Ahora se anula el asiento original. Los pares que ya existen (el
            // asiento revertido y su contraasiento) pasan los dos a Anulado: el saldo de
            // cada cuenta no cambia, porque entre ambos sumaban cero.
            migrationBuilder.Sql("""
                UPDATE contabilidad.asientos_contables
                SET estado = 'Anulado'
                WHERE estado = 'Confirmado'
                  AND (asiento_revertido_id IS NOT NULL
                       OR "Id" IN (SELECT asiento_revertido_id FROM contabilidad.asientos_contables
                                   WHERE asiento_revertido_id IS NOT NULL AND estado = 'Confirmado'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin vuelta atrás: la base no permite que un asiento Anulado vuelva a Confirmado.
        }
    }
}
