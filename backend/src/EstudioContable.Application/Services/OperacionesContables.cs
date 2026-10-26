using EstudioContable.Domain.Enums;
using System.Globalization;
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
        /// Antes de confirmar controla que la Caja no quede en negativo: si la acción
        /// la baja por debajo de cero se lanza un error y no se guarda nada.
        /// </summary>
        public static async Task EnTransaccionAsync(IAppDbContext context, Func<Task> accion, CancellationToken cancellationToken)
        {
            var saldoCajaAntes = await SaldoCajaAsync(context, cancellationToken);

            if (context.Database.CurrentTransaction != null)
            {
                await accion();
                await VerificarSaldoCajaAsync(context, saldoCajaAntes, cancellationToken);
                return;
            }

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await accion();
            await VerificarSaldoCajaAsync(context, saldoCajaAntes, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        private static async Task<decimal> SaldoCajaAsync(IAppDbContext context, CancellationToken cancellationToken)
            => await context.MovimientosCaja
                .SumAsync(m => m.TipoMovimiento == TipoMovimientoCaja.Ingreso ? m.Importe : -m.Importe, cancellationToken);

        /// <summary>
        /// No se puede sacar de la caja más efectivo del que hay. Si la caja ya estaba en
        /// negativo (datos anteriores) se permiten las operaciones que no la empeoran.
        /// </summary>
        private static async Task VerificarSaldoCajaAsync(IAppDbContext context, decimal saldoAntes, CancellationToken cancellationToken)
        {
            var saldoDespues = await SaldoCajaAsync(context, cancellationToken);
            if (saldoDespues < 0 && saldoDespues < saldoAntes)
            {
                var ar = CultureInfo.GetCultureInfo("es-AR");
                throw new InvalidOperationException(
                    $"Saldo insuficiente en Caja: hay {Math.Max(saldoAntes, 0).ToString("C2", ar)} y la operación necesita " +
                    $"{(saldoAntes - saldoDespues).ToString("C2", ar)}. Registrá primero el ingreso de efectivo o usá otro medio de pago.");
            }
        }
    }
}
