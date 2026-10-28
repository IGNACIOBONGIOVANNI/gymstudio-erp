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
    public record DatosMovimientoTesoreria(DateTime Fecha, string Detalle, bool EsIngreso, decimal Importe, long? CuentaContrapartidaId,
        long? ClienteComercialId = null, long? ProveedorId = null);

    /// <summary>Paso de fondos de un libro al otro (ej. depósito de efectivo en el banco).</summary>
    public record DatosTransferencia(LibroTesoreria Origen, DateTime Fecha, decimal Importe, string? Detalle);

    public interface ITesoreriaService
    {
        Task TransferirAsync(DatosTransferencia datos, CancellationToken cancellationToken);
        Task<long> CrearAsync(LibroTesoreria libro, DatosMovimientoTesoreria datos, CancellationToken cancellationToken);
        Task EditarAsync(LibroTesoreria libro, long id, DatosMovimientoTesoreria datos, CancellationToken cancellationToken);
        Task EliminarAsync(LibroTesoreria libro, long id, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Movimientos manuales de Caja y Banco. Cada uno genera su asiento (Caja/Banco contra
    /// la cuenta de contrapartida elegida), así el Libro Caja coincide siempre con el
    /// Mayor y el Balance. Editar o eliminar anula su asiento, igual que con las
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

            var (clienteCobrado, proveedorPagado) = await ResolverTerceroAsync(contrapartida, datos, movimiento, cancellationToken);
            movimiento.AsignarTercero(clienteCobrado, proveedorPagado);

            await OperacionesContables.EnTransaccionAsync(_context, async () =>
            {
                if (movimiento is MovimientoCaja caja) _context.MovimientosCaja.Add(caja);
                else _context.MovimientosBanco.Add((MovimientoBanco)movimiento);
                await _context.SaveChangesAsync(cancellationToken);

                await RegistrarAsientoAsync(libro, movimiento, contrapartida, cancellationToken);
            }, cancellationToken);

            return movimiento.Id;
        }

        /// <summary>
        /// Registra la transferencia: egreso en el libro de origen, ingreso en el de destino
        /// y un único asiento (Debe destino / Haber origen) al que apuntan los dos movimientos.
        /// </summary>
        public async Task TransferirAsync(DatosTransferencia datos, CancellationToken cancellationToken)
        {
            if (!Enum.IsDefined(datos.Origen))
                throw new ArgumentException("El origen de la transferencia debe ser Caja o Banco.");
            if (datos.Importe <= 0)
                throw new ArgumentException("El importe de la transferencia debe ser mayor a cero.");

            var destino = datos.Origen == LibroTesoreria.Caja ? LibroTesoreria.Banco : LibroTesoreria.Caja;
            var detalle = $"Transferencia de {datos.Origen} a {destino}";
            if (!string.IsNullOrWhiteSpace(datos.Detalle))
                detalle += $" - {datos.Detalle.Trim()}";

            var clienteId = PlanDeCuentas.ClienteIdPorDefecto.ToString();
            IMovimientoTesoreria salida = CrearMovimiento(datos.Origen, clienteId, datos.Fecha, detalle, esIngreso: false, datos.Importe);
            IMovimientoTesoreria entrada = CrearMovimiento(destino, clienteId, datos.Fecha, detalle, esIngreso: true, datos.Importe);

            await OperacionesContables.EnTransaccionAsync(_context, async () =>
            {
                Agregar(salida);
                Agregar(entrada);
                await _context.SaveChangesAsync(cancellationToken);

                var asiento = new AsientoContable(
                    PlanDeCuentas.ClienteIdPorDefecto,
                    await OperacionesContables.SiguienteNumeroAsientoAsync(_context, cancellationToken),
                    DateOnly.FromDateTime(datos.Fecha),
                    detalle.Length > 500 ? detalle[..500] : detalle,
                    PlanDeCuentas.UsuarioSistemaId);
                asiento.AgregarLinea(CuentaDelLibro(destino), datos.Importe, 0, detalle);
                asiento.AgregarLinea(CuentaDelLibro(datos.Origen), 0, datos.Importe, detalle);
                asiento.Confirmar(PlanDeCuentas.UsuarioSistemaId);
                _context.AsientosContables.Add(asiento);
                await _context.SaveChangesAsync(cancellationToken);

                salida.AsignarAsiento(asiento.Id);
                entrada.AsignarAsiento(asiento.Id);
                await _context.SaveChangesAsync(cancellationToken);
            }, cancellationToken);
        }

        public async Task EditarAsync(LibroTesoreria libro, long id, DatosMovimientoTesoreria datos, CancellationToken cancellationToken)
        {
            var movimiento = await BuscarManualAsync(libro, id, cancellationToken);
            if (await ContraparteDeTransferenciaAsync(libro, movimiento, cancellationToken) != null)
                throw new InvalidOperationException(
                    "Las transferencias entre Caja y Banco no se editan: eliminala y cargala de nuevo (así los dos libros quedan siempre iguales).");
            var anterior = await ContrapartidaActualAsync(libro, movimiento, cancellationToken);
            var contrapartida = ResolverContrapartida(datos, anterior);
            var (clienteCobrado, proveedorPagado) = await ResolverTerceroAsync(contrapartida, datos, movimiento, cancellationToken);

            await OperacionesContables.EnTransaccionAsync(_context, async () =>
            {
                if (movimiento.AsientoContableId is long asientoId)
                    await OperacionesContables.AnularAsientoAsync(_context, asientoId, cancellationToken);

                movimiento.ActualizarDatos(datos.Fecha, datos.Detalle, datos.EsIngreso, datos.Importe);
                movimiento.AsignarTercero(clienteCobrado, proveedorPagado);
                await _context.SaveChangesAsync(cancellationToken);

                await RegistrarAsientoAsync(libro, movimiento, contrapartida, cancellationToken);
            }, cancellationToken);
        }

        public async Task EliminarAsync(LibroTesoreria libro, long id, CancellationToken cancellationToken)
        {
            var movimiento = await BuscarManualAsync(libro, id, cancellationToken);
            // Si es una transferencia, se elimina también el movimiento del otro libro.
            var contraparte = await ContraparteDeTransferenciaAsync(libro, movimiento, cancellationToken);

            await OperacionesContables.EnTransaccionAsync(_context, async () =>
            {
                if (movimiento.AsientoContableId is long asientoId)
                    await OperacionesContables.AnularAsientoAsync(_context, asientoId, cancellationToken);

                Quitar(movimiento);
                if (contraparte != null)
                    Quitar(contraparte);

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

        /// <summary>
        /// El movimiento del otro libro que comparte el mismo asiento (sólo existe en las
        /// transferencias entre Caja y Banco).
        /// </summary>
        private async Task<IMovimientoTesoreria?> ContraparteDeTransferenciaAsync(LibroTesoreria libro, IMovimientoTesoreria movimiento, CancellationToken cancellationToken)
        {
            if (movimiento.AsientoContableId is not long asientoId)
                return null;

            return libro == LibroTesoreria.Caja
                ? await _context.MovimientosBanco.FirstOrDefaultAsync(m => m.AsientoContableId == asientoId, cancellationToken)
                : await _context.MovimientosCaja.FirstOrDefaultAsync(m => m.AsientoContableId == asientoId, cancellationToken);
        }

        private static IMovimientoTesoreria CrearMovimiento(LibroTesoreria libro, string clienteId, DateTime fecha, string detalle, bool esIngreso, decimal importe)
            => libro == LibroTesoreria.Caja
                ? new MovimientoCaja(clienteId, fecha, detalle, esIngreso ? TipoMovimientoCaja.Ingreso : TipoMovimientoCaja.Egreso, importe)
                : new MovimientoBanco(clienteId, fecha, detalle, esIngreso ? TipoMovimientoBanco.Ingreso : TipoMovimientoBanco.Egreso, importe);

        private void Agregar(IMovimientoTesoreria movimiento)
        {
            if (movimiento is MovimientoCaja caja) _context.MovimientosCaja.Add(caja);
            else _context.MovimientosBanco.Add((MovimientoBanco)movimiento);
        }

        private void Quitar(IMovimientoTesoreria movimiento)
        {
            if (movimiento is MovimientoCaja caja) _context.MovimientosCaja.Remove(caja);
            else _context.MovimientosBanco.Remove((MovimientoBanco)movimiento);
        }

        /// <summary>
        /// Un cobro a clientes (Deudores por Ventas) debe decir de qué cliente es, y un pago a proveedores,
        /// a qué proveedor; así se arma la cuenta corriente de cada uno. Con cualquier otra contrapartida
        /// no se guarda ninguno. Al editar sin indicarlo se conserva el que ya tenía.
        /// </summary>
        private async Task<(long? ClienteId, long? ProveedorId)> ResolverTerceroAsync(long contrapartida, DatosMovimientoTesoreria datos, IMovimientoTesoreria movimiento, CancellationToken cancellationToken)
        {
            if (contrapartida == PlanDeCuentas.DeudoresPorVentas)
            {
                var id = datos.ClienteComercialId ?? movimiento.ClienteComercialId
                    ?? throw new ArgumentException("Elegí el cliente al que corresponde el cobro.");
                var cliente = await _context.ClientesComerciales.AsNoTracking().Where(c => c.Id == id).Select(c => new { c.RazonSocial, c.Activo }).FirstOrDefaultAsync(cancellationToken)
                    ?? throw new ArgumentException("El cliente elegido no existe.");
                if (!cliente.Activo && id != movimiento.ClienteComercialId)
                    throw new InvalidOperationException($"El cliente {cliente.RazonSocial} está dado de baja.");
                return (id, null);
            }

            if (contrapartida == PlanDeCuentas.Proveedores)
            {
                var id = datos.ProveedorId ?? movimiento.ProveedorId
                    ?? throw new ArgumentException("Elegí el proveedor al que corresponde el pago.");
                var proveedor = await _context.Proveedores.AsNoTracking().Where(p => p.Id == id).Select(p => new { p.RazonSocial, p.Activo }).FirstOrDefaultAsync(cancellationToken)
                    ?? throw new ArgumentException("El proveedor elegido no existe.");
                if (!proveedor.Activo && id != movimiento.ProveedorId)
                    throw new InvalidOperationException($"El proveedor {proveedor.RazonSocial} está dado de baja.");
                return (null, id);
            }

            return (null, null);
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
