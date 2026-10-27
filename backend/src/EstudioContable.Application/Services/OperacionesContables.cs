using EstudioContable.Domain.Enums;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Common;
using EstudioContable.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using EstadoAsiento = EstudioContable.Domain.Entities.EstadoAsiento;

namespace EstudioContable.Application.Services
{
    /// <summary>
    /// Operaciones contables que comparten facturación y tesorería: numerar asientos,
    /// anularlos y agrupar varios cambios en una transacción.
    /// </summary>
    internal static class OperacionesContables
    {
        public static async Task<long> SiguienteNumeroAsientoAsync(IAppDbContext context, CancellationToken cancellationToken)
        {
            // Los asientos agregados en este contexto y aún no guardados también cuentan.
            var maxGuardado = await context.AsientosContables
                .Where(a => a.ClienteId == PlanDeCuentas.ClienteIdPorDefecto)
                .MaxAsync(a => (long?)a.NumeroAsiento, cancellationToken) ?? 0;

            var maxPendiente = context.AsientosContables.Local
                .Where(a => a.ClienteId == PlanDeCuentas.ClienteIdPorDefecto)
                .Select(a => a.NumeroAsiento)
                .DefaultIfEmpty(0)
                .Max();

            return Math.Max(maxGuardado, maxPendiente) + 1;
        }

        /// <summary>
        /// Deja sin efecto un asiento confirmado pasándolo a Anulado. No se borra (queda
        /// en el Diario marcado como anulado y en la auditoría) y no se genera un
        /// contraasiento: el Mayor y el Balance sólo suman asientos confirmados, así que
        /// las cuentas no muestran importes invertidos (por ejemplo, Haber en el IVA
        /// Crédito Fiscal).
        /// </summary>
        public static async Task AnularAsientoAsync(IAppDbContext context, long asientoId, CancellationToken cancellationToken)
        {
            var asiento = await context.AsientosContables
                .FirstOrDefaultAsync(a => a.Id == asientoId, cancellationToken);

            if (asiento == null || asiento.Estado != EstadoAsiento.Confirmado)
                return;

            asiento.Anular();
        }

        /// <summary>
        /// Ejecuta la acción en una transacción, salvo que el llamador ya haya abierto
        /// una (ej. el cobro de membresías), en cuyo caso participa de esa.
        /// </summary>
        public static async Task EnTransaccionAsync(IAppDbContext context, Func<Task> accion, CancellationToken cancellationToken)
        {
            // La Caja puede quedar en negativo (por ejemplo, si se cargan las compras de
            // contado antes que las ventas del mismo día): la operación se registra y el
            // frontend avisa el saldo. Los libros cierran igual al terminar de cargar.
            if (context.Database.CurrentTransaction != null)
            {
                await accion();
                return;
            }

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await accion();
            await transaction.CommitAsync(cancellationToken);
        }
    }
}
