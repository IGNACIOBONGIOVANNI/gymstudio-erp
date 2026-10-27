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
using EstadoAsiento = EstudioContable.Domain.Entities.EstadoAsiento;

namespace EstudioContable.Application.Services
{
    public class FacturacionService : IFacturacionService
    {
        private const string Venta = "Venta";
        private const string Compra = "Compra";
        private const string ContadoCaja = "ContadoCaja";
        private const string TransferenciaBanco = "TransferenciaBanco";
        private const string CuentaCorriente = "CuentaCorriente";
        private const decimal ImporteMaximo = 999_999_999_999.99m;
        private static readonly string[] CondicionesDePago = { ContadoCaja, TransferenciaBanco, CuentaCorriente };
        private static readonly string[] TiposDeComprobante = { "A", "B", "C" };

        private readonly IAppDbContext _context;

        public FacturacionService(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<Factura> CrearFacturaAsync(Factura factura, CancellationToken cancellationToken)
        {
            ValidarYNormalizarImportes(factura);

            await OperacionesContables.EnTransaccionAsync(_context, async () =>
            {
                await AsignarOValidarNumeroAsync(factura, idExcluido: null, cancellationToken);
                _context.Facturas.Add(factura);
                await _context.SaveChangesAsync(cancellationToken); // genera factura.Id

                await AplicarEfectosAsync(factura, cancellationToken);
            }, cancellationToken);

            return factura;
        }

        public async Task AnularFacturaAsync(long facturaId, CancellationToken cancellationToken)
        {
            var factura = await CargarFacturaAsync(facturaId, cancellationToken);
            if (factura.Anulada)
                throw new InvalidOperationException($"La factura N° {factura.Id} ya está anulada.");

            await OperacionesContables.EnTransaccionAsync(_context, async () =>
            {
                await RevertirEfectosAsync(factura, $"Anulación factura N° {factura.Id}", cancellationToken);

                factura.Anulada = true;
                factura.FechaAnulacion = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }, cancellationToken);
        }

        public async Task ReemplazarFacturaAsync(long facturaId, Factura datosNuevos, CancellationToken cancellationToken)
        {
            var factura = await CargarFacturaAsync(facturaId, cancellationToken);
            if (factura.Anulada)
                throw new InvalidOperationException($"La factura N° {factura.Id} está anulada y no puede modificarse.");

            ValidarYNormalizarImportes(datosNuevos);

            await OperacionesContables.EnTransaccionAsync(_context, async () =>
            {
                await RevertirEfectosAsync(factura, $"Modificación factura N° {factura.Id}", cancellationToken);

                factura.Fecha = datosNuevos.Fecha;
                factura.TipoOperacion = datosNuevos.TipoOperacion;
                factura.CondicionPago = datosNuevos.CondicionPago;
                // Una venta conserva su número salvo que cambie el tipo de comprobante;
                // una compra toma el número que se haya corregido.
                var cambiaNumeracion = datosNuevos.TipoOperacion == Compra
                    || factura.TipoOperacion != datosNuevos.TipoOperacion
                    || factura.TipoComprobante != datosNuevos.TipoComprobante;
                factura.TipoComprobante = datosNuevos.TipoComprobante;
                factura.Cliente = datosNuevos.Cliente;
                if (cambiaNumeracion)
                {
                    factura.PuntoVenta = datosNuevos.PuntoVenta;
                    factura.NumeroComprobante = datosNuevos.NumeroComprobante;
                    await AsignarOValidarNumeroAsync(factura, idExcluido: factura.Id, cancellationToken);
                }
                factura.Subtotal = datosNuevos.Subtotal;
                factura.Iva = datosNuevos.Iva;
                factura.Total = datosNuevos.Total;

                _context.FacturaItems.RemoveRange(factura.Items);
                factura.Items.Clear();
                foreach (var item in datosNuevos.Items)
                    factura.Items.Add(item);

                await _context.SaveChangesAsync(cancellationToken);
                await AplicarEfectosAsync(factura, cancellationToken);
            }, cancellationToken);
        }

        /// <summary>
        /// Genera asiento, movimiento de tesorería y movimiento de stock para el
        /// estado actual de la factura. La factura ya debe tener Id.
        /// </summary>
        private async Task AplicarEfectosAsync(Factura factura, CancellationToken cancellationToken)
        {
            await VincularProductosAsync(factura, cancellationToken);

            var esVenta = factura.TipoOperacion == Venta;
            // El asiento lleva el número del comprobante para cruzarlo con el Libro IVA.
            var detalle = $"{factura.TipoOperacion} - Factura {factura.TipoComprobante} {factura.NumeroFormateado ?? $"N° {factura.Id}"} - {factura.Cliente}";

            // 1. Asiento de partida doble
            var asiento = new AsientoContable(
                PlanDeCuentas.ClienteIdPorDefecto,
                await OperacionesContables.SiguienteNumeroAsientoAsync(_context, cancellationToken),
                DateOnly.FromDateTime(factura.Fecha),
                detalle,
                PlanDeCuentas.UsuarioSistemaId);

            var cuentaContrapartida = factura.CondicionPago switch
            {
                ContadoCaja => PlanDeCuentas.Caja,
                TransferenciaBanco => PlanDeCuentas.Banco,
                _ => esVenta ? PlanDeCuentas.DeudoresPorVentas : PlanDeCuentas.Proveedores
            };

            if (esVenta)
            {
                asiento.AgregarLinea(cuentaContrapartida, factura.Total, 0, "Total Factura");
                asiento.AgregarLinea(PlanDeCuentas.Ventas, 0, factura.Subtotal, "Ventas");
                if (factura.Iva > 0)
                    asiento.AgregarLinea(PlanDeCuentas.IvaDebitoFiscal, 0, factura.Iva, "IVA Débito Fiscal");
            }
            else
            {
                asiento.AgregarLinea(PlanDeCuentas.Compras, factura.Subtotal, 0, "Compras");
                if (factura.Iva > 0)
                    asiento.AgregarLinea(PlanDeCuentas.IvaCreditoFiscal, factura.Iva, 0, "IVA Crédito Fiscal");
                asiento.AgregarLinea(cuentaContrapartida, 0, factura.Total, "Total Factura");
            }

            asiento.Confirmar(PlanDeCuentas.UsuarioSistemaId);
            _context.AsientosContables.Add(asiento);
            await _context.SaveChangesAsync(cancellationToken);
            factura.AsientoContableId = asiento.Id;

            // 2. Tesorería: una venta cobrada es ingreso, una compra pagada es egreso
            RegistrarMovimientoTesoreria(factura, esIngreso: esVenta, detalle, factura.Fecha);

            // 3. Stock: la venta descuenta, la compra suma
            await RegistrarMovimientosStockAsync(factura, esSalida: esVenta, detalle, factura.Fecha, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Deshace los efectos del estado actual de la factura: anula su asiento,
        /// registra el movimiento de tesorería opuesto y devuelve el stock.
        /// </summary>
        private async Task RevertirEfectosAsync(Factura factura, string motivo, CancellationToken cancellationToken)
        {
            var esVenta = factura.TipoOperacion == Venta;

            // 1. El asiento vigente pasa a Anulado (la factura anulada sigue apuntándolo).
            if (factura.AsientoContableId is long asientoId)
                await OperacionesContables.AnularAsientoAsync(_context, asientoId, cancellationToken);

            // 2. Tesorería y 3. stock, en sentido opuesto al original y con fecha
            // de hoy (la devolución de fondos/mercadería ocurre al anular).
            var hoy = DateTime.UtcNow;
            RegistrarMovimientoTesoreria(factura, esIngreso: !esVenta, motivo, hoy);
            await RegistrarMovimientosStockAsync(factura, esSalida: !esVenta, motivo, hoy, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
        }

        private void RegistrarMovimientoTesoreria(Factura factura, bool esIngreso, string detalle, DateTime fecha)
        {
            var clienteId = PlanDeCuentas.ClienteIdPorDefecto.ToString();

            if (factura.CondicionPago == ContadoCaja)
            {
                var tipo = esIngreso ? TipoMovimientoCaja.Ingreso : TipoMovimientoCaja.Egreso;
                _context.MovimientosCaja.Add(new MovimientoCaja(clienteId, fecha, detalle, tipo, factura.Total, factura.Id));
            }
            else if (factura.CondicionPago == TransferenciaBanco)
            {
                var tipo = esIngreso ? TipoMovimientoBanco.Ingreso : TipoMovimientoBanco.Egreso;
                _context.MovimientosBanco.Add(new MovimientoBanco(clienteId, fecha, detalle, tipo, factura.Total, factura.Id));
            }
            // Cuenta corriente: no hay movimiento de fondos.
        }

        /// <summary>
        /// Un ítem sin producto elegido se vincula al producto del inventario con ese
        /// mismo nombre (sin distinguir mayúsculas). En una compra, si no existe y se
        /// pidió, se da de alta: así la mercadería comprada entra al Libro Stock.
        /// </summary>
        private async Task VincularProductosAsync(Factura factura, CancellationToken cancellationToken)
        {
            foreach (var item in factura.Items.Where(i => i.ProductoId is null or 0))
            {
                var nombre = item.Descripcion.Trim();
                var nombreBuscado = nombre.ToLower();
                var existente = await _context.Productos
                    .FirstOrDefaultAsync(p => p.Nombre.Trim().ToLower() == nombreBuscado, cancellationToken);

                if (existente == null && factura.TipoOperacion == Compra && item.AgregarAlInventario)
                {
                    existente = new Producto(nombre, item.PrecioUnitario, precioVenta: 0);
                    _context.Productos.Add(existente);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                item.ProductoId = existente?.Id;
            }
        }

        private async Task RegistrarMovimientosStockAsync(Factura factura, bool esSalida, string detalle, DateTime fecha, CancellationToken cancellationToken)
        {
            foreach (var item in factura.Items.Where(i => i.ProductoId is > 0))
            {
                var producto = await _context.Productos.FindAsync(new object[] { item.ProductoId!.Value }, cancellationToken);
                if (producto == null)
                    throw new NotFoundException(nameof(Producto), item.ProductoId!.Value);

                producto.ActualizarStock(esSalida ? -item.Cantidad : item.Cantidad);
                if (factura.TipoOperacion == Venta && esSalida)
                    producto.CompletarPrecioVenta(item.PrecioUnitario);

                _context.MovimientosStock.Add(new MovimientoStock(
                    producto.Id,
                    new DateTimeOffset(DateTime.SpecifyKind(fecha, DateTimeKind.Utc)),
                    detalle,
                    ingreso: esSalida ? 0 : item.Cantidad,
                    salida: esSalida ? item.Cantidad : 0,
                    saldo: producto.StockActual));
            }
        }

        /// <summary>
        /// El backend no confía en los importes que manda el cliente: recalcula el
        /// subtotal de cada ítem y exige que Subtotal = Σ ítems y Total = Subtotal + IVA.
        /// </summary>
        private static void ValidarYNormalizarImportes(Factura factura)
        {
            if (factura.TipoOperacion != Venta && factura.TipoOperacion != Compra)
                throw new ArgumentException($"Tipo de operación inválido: '{factura.TipoOperacion}'. Debe ser '{Venta}' o '{Compra}'.");

            if (factura.Fecha.Year < 2000 || factura.Fecha.Year > 2100)
                throw new ArgumentException("La fecha de la factura es obligatoria y debe ser válida.");

            if (!CondicionesDePago.Contains(factura.CondicionPago))
                throw new ArgumentException("Condición de pago inválida: debe ser contado (caja), transferencia (banco) o cuenta corriente.");

            if (!TiposDeComprobante.Contains(factura.TipoComprobante?.Trim().ToUpperInvariant()))
                throw new ArgumentException("Tipo de comprobante inválido: debe ser A, B o C.");
            factura.TipoComprobante = factura.TipoComprobante!.Trim().ToUpperInvariant();

            factura.Cliente = (factura.Cliente ?? string.Empty).Trim();
            if (factura.Cliente.Length == 0)
                throw new ArgumentException(factura.TipoOperacion == Compra
                    ? "Indicá el proveedor de la factura."
                    : "Indicá el cliente de la factura (o \"Consumidor Final\").");
            if (factura.Cliente.Length > 200)
                throw new ArgumentException("El nombre del cliente o proveedor no puede superar los 200 caracteres.");

            if (factura.Items.Count == 0)
                throw new ArgumentException("La factura debe tener al menos un ítem.");

            foreach (var item in factura.Items)
            {
                if (item.Cantidad <= 0)
                    throw new ArgumentException($"La cantidad del ítem '{item.Descripcion}' debe ser mayor a cero.");
                if (item.PrecioUnitario < 0)
                    throw new ArgumentException($"El precio del ítem '{item.Descripcion}' no puede ser negativo.");

                item.Subtotal = Math.Round(item.Cantidad * item.PrecioUnitario, 2);
            }

            var subtotalItems = factura.Items.Sum(i => i.Subtotal);
            if (Math.Abs(subtotalItems - factura.Subtotal) > 0.01m)
                throw new ArgumentException($"El subtotal ({factura.Subtotal:N2}) no coincide con la suma de los ítems ({subtotalItems:N2}).");

            if (factura.Iva < 0)
                throw new ArgumentException("El IVA no puede ser negativo.");
            if (factura.TipoComprobante == "C" && factura.Iva != 0)
                throw new ArgumentException("La factura C no discrimina IVA: el IVA debe ser 0.");

            if (Math.Abs(factura.Subtotal + factura.Iva - factura.Total) > 0.01m)
                throw new ArgumentException($"El total ({factura.Total:N2}) no coincide con subtotal + IVA ({factura.Subtotal + factura.Iva:N2}).");

            factura.Subtotal = subtotalItems;
            factura.Iva = Math.Round(factura.Iva, 2);
            factura.Total = factura.Subtotal + factura.Iva;

            if (factura.Total <= 0)
                throw new ArgumentException("El total de la factura debe ser mayor a cero.");
            if (factura.Total > ImporteMaximo)
                throw new ArgumentException("El total de la factura es demasiado grande.");
        }

        /// <summary>
        /// Ventas: si no viene número, se asigna el siguiente correlativo del tipo y punto
        /// de venta. Compras: el número de la factura del proveedor es obligatorio y no
        /// puede cargarse dos veces la misma factura del mismo proveedor.
        /// </summary>
        private async Task AsignarOValidarNumeroAsync(Factura factura, long? idExcluido, CancellationToken cancellationToken)
        {
            if (factura.PuntoVenta is < 1 or > 99999)
                throw new ArgumentException("El punto de venta debe ser un número entre 1 y 99999.");
            if (factura.NumeroComprobante is < 1 or > 99999999)
                throw new ArgumentException("El número de comprobante debe ser un número entre 1 y 99999999.");

            if (factura.TipoOperacion == Venta)
            {
                factura.PuntoVenta ??= 1;
                if (factura.NumeroComprobante == null)
                {
                    var ultimo = await _context.Facturas
                        .Where(f => f.TipoOperacion == Venta && f.TipoComprobante == factura.TipoComprobante
                                    && f.PuntoVenta == factura.PuntoVenta && f.Id != idExcluido)
                        .MaxAsync(f => f.NumeroComprobante, cancellationToken);
                    factura.NumeroComprobante = (ultimo ?? 0) + 1;
                    return;
                }

                var repetida = await _context.Facturas.AnyAsync(f =>
                    f.TipoOperacion == Venta && f.TipoComprobante == factura.TipoComprobante && f.PuntoVenta == factura.PuntoVenta
                    && f.NumeroComprobante == factura.NumeroComprobante && f.Id != idExcluido, cancellationToken);
                if (repetida)
                    throw new InvalidOperationException($"Ya existe la factura de venta {factura.TipoComprobante} {factura.NumeroFormateado}.");
                return;
            }

            if (factura.PuntoVenta == null || factura.NumeroComprobante == null)
                throw new ArgumentException("Indicá el punto de venta y el número de la factura del proveedor (ej. 0003-00001234).");

            var duplicada = await _context.Facturas.AnyAsync(f =>
                f.TipoOperacion == Compra && !f.Anulada && f.Id != idExcluido
                && f.TipoComprobante == factura.TipoComprobante && f.PuntoVenta == factura.PuntoVenta
                && f.NumeroComprobante == factura.NumeroComprobante
                && (factura.ProveedorId != null ? f.ProveedorId == factura.ProveedorId : f.Cliente == factura.Cliente), cancellationToken);
            if (duplicada)
                throw new InvalidOperationException(
                    $"La factura {factura.TipoComprobante} {factura.NumeroFormateado} de {factura.Cliente} ya está cargada.");
        }

        private async Task<Factura> CargarFacturaAsync(long facturaId, CancellationToken cancellationToken)
        {
            return await _context.Facturas
                .Include(f => f.Items)
                .FirstOrDefaultAsync(f => f.Id == facturaId, cancellationToken)
                ?? throw new NotFoundException(nameof(Factura), facturaId);
        }
    }
}
