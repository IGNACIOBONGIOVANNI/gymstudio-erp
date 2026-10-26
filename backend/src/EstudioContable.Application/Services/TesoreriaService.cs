using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Common;
using EstudioContable.Application.Common.Exceptions;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Services
{
    public enum LibroTesoreria { Caja, Banco }

    /// <summary>Datos de un movimiento cargado a mano en el Libro Caja o Banco.</summary>
    public record DatosMovimientoTesoreria(DateTime Fecha, string Detalle, bool EsIngreso, decimal Importe, long? CuentaContrapartidaId);

    public interface ITesoreriaService
    {
        Task<long> CrearAsync(LibroTesoreria libro, DatosMovimientoTesoreria datos, CancellationToken cancellationToken);
        Task EditarAsync(LibroTesoreria libro, long id, DatosMovimientoTesoreria datos, CancellationToken cancellationToken);
        Task EliminarAsync(LibroTesoreria libro, long id, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Movimientos manuales de Caja y Banco. Cada uno genera su asiento (Caja/Banco contra
    /// la cuenta de contrapartida elegida), así el Libro Caja coincide siempre con el
    /// Mayor y el Balance. Editar o eliminar registra un contraasiento, igual que con las
    /// facturas. Los movimientos que generó una factura sólo se corrigen desde la factura.
    /// </summary>
    public class TesoreriaService : ITesoreriaService
    {
        private readonly IAppDbContext _context;

        public TesoreriaService(IAppDbContext context)
        {
            _context = context;
        }

        // Contrapartidas posibles para un movimiento manual. La primera de cada lista es
        // la que se usa por defecto.
        private static readonly long[] ContrapartidasIngreso = { PlanDeCuentas.OtrosIngresos, PlanDeCuentas.DeudoresPorVentas };
        private static readonly long[] ContrapartidasEgreso = { PlanDeCuentas.GastosGenerales, PlanDeCuentas.Proveedores };

        public async Task<long> CrearAsync(LibroTesoreria libro, DatosMovimientoTesoreria datos, CancellationToken cancellationToken)
        {
            var contrapartida = ResolverContrapartida(datos, anterior: null);
            var clienteId = PlanDeCuentas.ClienteIdPorDefecto.ToString();

            IMovimientoTesoreria movimiento = libro == LibroTesoreria.Caja
                ? new MovimientoCaja(clienteId, datos.Fecha, datos.Detalle, datos.EsIngreso ? TipoMovimientoCaja.Ingreso : TipoMovimientoCaja.Egreso, datos.Importe)
                : new MovimientoBanco(clienteId, datos.Fecha, datos.Detalle, datos.EsIngreso ? TipoMovimientoBanco.Ingreso : TipoMovimientoBanco.Egreso, datos.Importe);

            await OperacionesContables.EnTransaccionAsync(_context, async () =>
            {
                if (movimiento is MovimientoCaja caja) _context.MovimientosCaja.Add(caja);
                else _context.MovimientosBanco.Add((MovimientoBanco)movimiento);
                await _context.SaveChangesAsync(cancellationToken);

                await RegistrarAsientoAsync(libro, movimiento, contrapartida, cancellationToken);
            }, cancellationToken);

            return movimiento.Id;
        }

        public async Task EditarAsync(LibroTesoreria libro, long id, DatosMovimientoTesoreria datos, CancellationToken cancellationToken)
        {
            var movimiento = await BuscarManualAsync(libro, id, cancellationToken);
            var anterior = await ContrapartidaActualAsync(libro, movimiento, cancellationToken);
            var contrapartida = ResolverContrapartida(datos, anterior);

            await OperacionesContables.EnTransaccionAsync(_context, async () =>
            {
                if (movimiento.AsientoContableId is long asientoId)
                    await OperacionesContables.RevertirAsientoAsync(_context, asientoId, cancellationToken);

                movimiento.ActualizarDatos(datos.Fecha, datos.Detalle, datos.EsIngreso, datos.Importe);
                await _context.SaveChangesAsync(cancellationToken);

                await RegistrarAsientoAsync(libro, movimiento, contrapartida, cancellationToken);
            }, cancellationToken);
        }

        public async Task EliminarAsync(LibroTesoreria libro, long id, CancellationToken cancellationToken)
        {
            var movimiento = await BuscarManualAsync(libro, id, cancellationToken);

            await OperacionesContables.EnTransaccionAsync(_context, async () =>
            {
                if (movimiento.AsientoContableId is long asientoId)
                    await OperacionesContables.RevertirAsientoAsync(_context, asientoId, cancellationToken);

                if (movimiento is MovimientoCaja caja) _context.MovimientosCaja.Remove(caja);
                else _context.MovimientosBanco.Remove((MovimientoBanco)movimiento);

                await _context.SaveChangesAsync(cancellationToken);
            }, cancellationToken);
        }

        private async Task RegistrarAsientoAsync(LibroTesoreria libro, IMovimientoTesoreria movimiento, long contrapartida, CancellationToken cancellationToken)
        {
            var cuentaFondos = CuentaDelLibro(libro);
            var descripcion = $"{libro} - {movimiento.Detalle}";
            if (descripcion.Length > 500) descripcion = descripcion[..500];

            var asiento = new AsientoContable(
                PlanDeCuentas.ClienteIdPorDefecto,
                await OperacionesContables.SiguienteNumeroAsientoAsync(_context, cancellationToken),
                DateOnly.FromDateTime(movimiento.Fecha),
                descripcion,
                PlanDeCuentas.UsuarioSistemaId);

            // Un ingreso aumenta Caja/Banco (Debe); un egreso la disminuye (Haber).
            if (movimiento.EsIngreso)
            {
                asiento.AgregarLinea(cuentaFondos, movimiento.Importe, 0, movimiento.Detalle);
                asiento.AgregarLinea(contrapartida, 0, movimiento.Importe, movimiento.Detalle);
            }
            else
            {
                asiento.AgregarLinea(contrapartida, movimiento.Importe, 0, movimiento.Detalle);
                asiento.AgregarLinea(cuentaFondos, 0, movimiento.Importe, movimiento.Detalle);
            }

            asiento.Confirmar(PlanDeCuentas.UsuarioSistemaId);
            _context.AsientosContables.Add(asiento);
            await _context.SaveChangesAsync(cancellationToken);

            movimiento.AsignarAsiento(asiento.Id);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<IMovimientoTesoreria> BuscarManualAsync(LibroTesoreria libro, long id, CancellationToken cancellationToken)
        {
            IMovimientoTesoreria? movimiento = libro == LibroTesoreria.Caja
                ? await _context.MovimientosCaja.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
                : await _context.MovimientosBanco.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

            if (movimiento == null)
                throw new NotFoundException(libro == LibroTesoreria.Caja ? "Movimiento de caja" : "Movimiento de banco", id);

            if (movimiento.FacturaId is long facturaId)
                throw new InvalidOperationException(
                    $"Este movimiento lo generó la factura N° {facturaId}. Para corregirlo, editá o anulá la factura desde el Libro de Ventas o de Compras.");

            return movimiento;
        }

        /// <summary>Cuenta de contrapartida del asiento vigente del movimiento (para conservarla al editar).</summary>
        private async Task<long?> ContrapartidaActualAsync(LibroTesoreria libro, IMovimientoTesoreria movimiento, CancellationToken cancellationToken)
        {
            if (movimiento.AsientoContableId is not long asientoId)
                return null;

            var cuentaFondos = CuentaDelLibro(libro);
            return await _context.LineasAsiento
                .Where(l => l.AsientoId == asientoId && l.CuentaId != cuentaFondos)
                .Select(l => (long?)l.CuentaId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private static long ResolverContrapartida(DatosMovimientoTesoreria datos, long? anterior)
        {
            var permitidas = datos.EsIngreso ? ContrapartidasIngreso : ContrapartidasEgreso;

            if (datos.CuentaContrapartidaId is long elegida)
            {
                if (!permitidas.Contains(elegida))
                    throw new ArgumentException(datos.EsIngreso
                        ? "Para un ingreso la contrapartida debe ser Otros Ingresos o Deudores por Ventas."
                        : "Para un egreso la contrapartida debe ser Gastos Generales o Proveedores.");
                return elegida;
            }

            // Al editar sin elegir, se conserva la anterior si sigue siendo válida.
            return anterior is long a && permitidas.Contains(a) ? a : permitidas[0];
        }

        private static long CuentaDelLibro(LibroTesoreria libro)
            => libro == LibroTesoreria.Caja ? PlanDeCuentas.Caja : PlanDeCuentas.Banco;
    }
}
