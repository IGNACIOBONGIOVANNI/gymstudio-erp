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
    /// revertirlos con un contraasiento y agrupar varios cambios en una transacción.
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
        /// Registra el contraasiento de un asiento confirmado (si no fue revertido antes).
        /// No borra nada: el original y su reverso quedan en el Libro Diario.
        /// </summary>
        public static async Task RevertirAsientoAsync(IAppDbContext context, long asientoId, CancellationToken cancellationToken)
        {
            var original = await context.AsientosContables
                .Include(a => a.Lineas)
                .FirstOrDefaultAsync(a => a.Id == asientoId, cancellationToken);

            var yaRevertido = await context.AsientosContables
                .AnyAsync(a => a.AsientoRevertidoId == asientoId, cancellationToken);

            if (original == null || original.Estado != EstadoAsiento.Confirmado || yaRevertido)
                return;

            var reverso = original.CrearContraasiento(
                await SiguienteNumeroAsientoAsync(context, cancellationToken),
                DateOnly.FromDateTime(DateTime.UtcNow),
                PlanDeCuentas.UsuarioSistemaId);

            reverso.Confirmar(PlanDeCuentas.UsuarioSistemaId);
            context.AsientosContables.Add(reverso);
        }

        /// <summary>
        /// Ejecuta la acción en una transacción, salvo que el llamador ya haya abierto
        /// una (ej. el cobro de membresías), en cuyo caso participa de esa.
        /// </summary>
        public static async Task EnTransaccionAsync(IAppDbContext context, Func<Task> accion, CancellationToken cancellationToken)
        {
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
