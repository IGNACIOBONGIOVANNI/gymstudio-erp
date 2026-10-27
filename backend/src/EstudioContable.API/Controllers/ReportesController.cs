using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EstudioContable.Application.Common;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using System.Linq;

namespace EstudioContable.API.Controllers
{
    /// <summary>
    /// Datos de los libros que el frontend imprime en PDF. Cada reporte trae lo que el
    /// libro necesita para leerse solo: saldos iniciales del período, saldos corridos,
    /// números de comprobante y totales que se puedan controlar contra el Mayor.
    /// </summary>
    [ApiController]
    [Route("api/reportes")]
    public class ReportesController : ControllerBase
    {
        private readonly IAppDbContext _context;

        public ReportesController(IAppDbContext context)
        {
            _context = context;
        }

        [HttpGet("generar")]
        public async Task<IActionResult> GenerarReporte(
            [FromQuery] string tipo,
            [FromQuery] DateTime? desde,
            [FromQuery] DateTime? hasta,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(tipo)) return BadRequest("Tipo de reporte es requerido.");
            if (desde.HasValue && hasta.HasValue && desde.Value.Date > hasta.Value.Date)
                return BadRequest("La fecha desde no puede ser posterior a la fecha hasta.");

            DateOnly? desdeD = desde.HasValue ? DateOnly.FromDateTime(desde.Value) : null;
            DateOnly? hastaD = hasta.HasValue ? DateOnly.FromDateTime(hasta.Value) : null;

            switch (tipo.ToLower())
            {
                case "diario":
                {
                    // El Libro Diario sólo registra asientos confirmados (los borradores no son registraciones).
                    var queryDiario = _context.AsientosContables
                        .Include(a => a.Lineas)
                        .ThenInclude(l => l.Cuenta)
                        .AsNoTracking()
                        .Where(a => a.Estado == EstadoAsiento.Confirmado);
                    if (desdeD.HasValue) queryDiario = queryDiario.Where(x => x.Fecha >= desdeD.Value);
                    if (hastaD.HasValue) queryDiario = queryDiario.Where(x => x.Fecha <= hastaD.Value);

                    var asientos = await queryDiario.OrderBy(x => x.Fecha).ThenBy(x => x.NumeroAsiento).ToListAsync(cancellationToken);
                    return Ok(asientos.Select(a => new {
                        id = a.Id,
                        numeroAsiento = a.NumeroAsiento,
                        fecha = a.Fecha,
                        descripcion = a.Descripcion,
                        totalDebe = a.Lineas.Sum(l => l.Debe),
                        totalHaber = a.Lineas.Sum(l => l.Haber),
                        lineas = a.Lineas.OrderBy(l => l.Orden).Select(l => new {
                            cuentaId = l.CuentaId,
                            codigoCuenta = l.Cuenta != null ? l.Cuenta.Codigo : "",
                            nombreCuenta = l.Cuenta != null ? l.Cuenta.Nombre : $"Cuenta {l.CuentaId}",
                            debe = l.Debe,
                            haber = l.Haber
                        }).ToList()
                    }));
                }

                case "caja":
                {
                    var movimientos = await _context.MovimientosCaja.AsNoTracking()
                        .Where(x => !hasta.HasValue || x.Fecha.Date <= hasta.Value.Date)
                        .OrderBy(x => x.Fecha).ThenBy(x => x.Id)
                        .Select(m => new MovimientoTesoreria(m.Fecha, m.Detalle,
                            m.TipoMovimiento == EstudioContable.Domain.Enums.TipoMovimientoCaja.Ingreso ? m.Importe : 0,
                            m.TipoMovimiento == EstudioContable.Domain.Enums.TipoMovimientoCaja.Egreso ? m.Importe : 0))
                        .ToListAsync(cancellationToken);
                    return Ok(ConSaldos(movimientos, desde));
                }

                case "banco":
                {
                    var movimientos = await _context.MovimientosBanco.AsNoTracking()
                        .Where(x => !hasta.HasValue || x.Fecha.Date <= hasta.Value.Date)
                        .OrderBy(x => x.Fecha).ThenBy(x => x.Id)
                        .Select(m => new MovimientoTesoreria(m.Fecha, m.Detalle,
                            m.TipoMovimiento == EstudioContable.Domain.Enums.TipoMovimientoBanco.Ingreso ? m.Importe : 0,
                            m.TipoMovimiento == EstudioContable.Domain.Enums.TipoMovimientoBanco.Egreso ? m.Importe : 0))
                        .ToListAsync(cancellationToken);
                    return Ok(ConSaldos(movimientos, desde));
                }

                case "mayor":
                {
                    var queryLineas = _context.LineasAsiento
                        .Include(l => l.Cuenta)
                        .Include(l => l.Asiento)
                        .AsNoTracking()
                        .Where(l => l.Asiento.Estado == EstadoAsiento.Confirmado);
                    if (hastaD.HasValue) queryLineas = queryLineas.Where(x => x.Asiento.Fecha <= hastaD.Value);

                    var lineas = await queryLineas.ToListAsync(cancellationToken);
                    var mayor = lineas.GroupBy(l => new { l.CuentaId, l.Cuenta.Codigo, l.Cuenta.Nombre, l.Cuenta.Naturaleza })
                        .OrderBy(g => g.Key.Codigo)
                        .Select(g =>
                        {
                            var anteriores = g.Where(l => desdeD.HasValue && l.Asiento.Fecha < desdeD.Value).ToList();
                            var delPeriodo = g.Except(anteriores).ToList();
                            var saldoAnterior = anteriores.Sum(l => l.Debe) - anteriores.Sum(l => l.Haber);
                            return new
                            {
                                codigo = g.Key.Codigo,
                                cuenta = g.Key.Nombre,
                                naturaleza = g.Key.Naturaleza.ToString(),
                                saldoAnterior,
                                totalDebe = delPeriodo.Sum(l => l.Debe),
                                totalHaber = delPeriodo.Sum(l => l.Haber),
                                // Saldo = Debe - Haber: positivo es deudor, negativo acreedor.
                                saldo = saldoAnterior + delPeriodo.Sum(l => l.Debe) - delPeriodo.Sum(l => l.Haber),
                                movimientos = delPeriodo
                                    .OrderBy(l => l.Asiento.Fecha).ThenBy(l => l.Asiento.NumeroAsiento).ThenBy(l => l.Orden)
                                    .Select(l => new {
                                        fecha = l.Asiento.Fecha,
                                        numeroAsiento = l.Asiento.NumeroAsiento,
                                        descripcion = l.Asiento.Descripcion,
                                        debe = l.Debe,
                                        haber = l.Haber
                                    }).ToList()
                            };
                        })
                        .Where(c => c.movimientos.Count > 0 || c.saldoAnterior != 0)
                        .ToList();

                    return Ok(mayor);
                }

                case "balance":
                {
                    // Balance de sumas y saldos: lo calcula contabilidad.fn_sumas_y_saldos.
                    var cuentas = await _context
                        .SumasYSaldos(PlanDeCuentas.ClienteIdPorDefecto, desdeD, hastaD)
                        .Where(c => c.TotalDebe != 0 || c.TotalHaber != 0)
                        .ToListAsync(cancellationToken);

                    return Ok(cuentas
                        .OrderBy(c => c.Codigo)
                        .Select(c =>
                        {
                            var saldo = c.TotalDebe - c.TotalHaber;
                            return new
                            {
                                codigo = c.Codigo,
                                cuenta = c.Nombre,
                                naturaleza = c.Naturaleza.ToString(),
                                totalDebe = c.TotalDebe,
                                totalHaber = c.TotalHaber,
                                saldoDeudor = saldo > 0 ? saldo : 0,
                                saldoAcreedor = saldo < 0 ? -saldo : 0
                            };
                        })
                        .ToList());
                }

                case "stock":
                {
                    // Libro Stock: una ficha por producto con ingresos (compras), salidas
                    // (ventas) y saldo corrido. El stock cargado al dar de alta el producto
                    // no tiene movimiento: entra como parte del saldo anterior.
                    var productos = await _context.Productos.AsNoTracking().OrderBy(p => p.Nombre).ToListAsync(cancellationToken);
                    // En orden de registro: así el saldo corrido es el que tenía el producto en
                    // cada momento (un stock inicial cargado hoy no queda después de una venta
                    // con fecha anterior mostrando un saldo negativo que nunca existió).
                    var movimientos = await _context.MovimientosStock.AsNoTracking()
                        .OrderBy(m => m.Id)
                        .ToListAsync(cancellationToken);

                    // Precio de venta: el del producto o, si no tiene (los que se dan de alta
                    // desde una compra), el de su última venta.
                    var ultimoPrecioVenta = (await _context.FacturaItems.AsNoTracking()
                            .Where(i => i.ProductoId != null && i.Factura.TipoOperacion == "Venta" && !i.Factura.Anulada)
                            .Select(i => new { ProductoId = i.ProductoId!.Value, i.Id, i.PrecioUnitario })
                            .ToListAsync(cancellationToken))
                        .GroupBy(i => i.ProductoId)
                        .ToDictionary(g => g.Key, g => g.OrderBy(i => i.Id).Last().PrecioUnitario);

                    return Ok(productos.Select(p =>
                    {
                        var delProducto = movimientos.Where(m => m.ProductoId == p.Id).ToList();
                        var stockSinMovimientos = p.StockActual - delProducto.Sum(m => m.Ingreso - m.Salida);
                        var anteriores = delProducto.Where(m => desde.HasValue && m.Fecha.UtcDateTime.Date < desde.Value.Date).ToList();
                        var delPeriodo = delProducto
                            .Where(m => (!desde.HasValue || m.Fecha.UtcDateTime.Date >= desde.Value.Date)
                                        && (!hasta.HasValue || m.Fecha.UtcDateTime.Date <= hasta.Value.Date))
                            .ToList();
                        var saldoAnterior = stockSinMovimientos + anteriores.Sum(m => m.Ingreso - m.Salida);
                        var saldo = saldoAnterior;
                        return new
                        {
                            producto = p.Nombre,
                            precioUnitario = p.PrecioUnitario,
                            precioVenta = p.PrecioVenta > 0 ? p.PrecioVenta : ultimoPrecioVenta.GetValueOrDefault(p.Id),
                            saldoAnterior,
                            movimientos = delPeriodo.Select(m =>
                            {
                                saldo += m.Ingreso - m.Salida;
                                return new { fecha = m.Fecha, detalle = m.Detalle, ingreso = m.Ingreso, salida = m.Salida, saldo };
                            }).ToList(),
                            saldoFinal = saldo
                        };
                    })
                    .Where(p => p.movimientos.Count > 0 || p.saldoAnterior != 0)
                    .ToList());
                }

                case "venta":
                case "compra":
                {
                    var operacion = tipo.ToLower() == "venta" ? "Venta" : "Compra";
                    var query = _context.Facturas.AsNoTracking()
                        .Include(f => f.ClienteObj).Include(f => f.ProveedorObj).Include(f => f.SocioObj)
                        .Where(f => f.TipoOperacion == operacion);
                    if (desde.HasValue) query = query.Where(x => x.Fecha.Date >= desde.Value.Date);
                    if (hasta.HasValue) query = query.Where(x => x.Fecha.Date <= hasta.Value.Date);

                    var facturas = await query
                        .OrderBy(x => x.Fecha).ThenBy(x => x.TipoComprobante).ThenBy(x => x.PuntoVenta)
                        .ThenBy(x => x.NumeroComprobante).ThenBy(x => x.Id)
                        .ToListAsync(cancellationToken);

                    return Ok(facturas.Select(f => new ReporteLibroIvaDto
                    {
                        Fecha = f.Fecha,
                        Comprobante = string.IsNullOrEmpty(f.TipoComprobante) ? "Factura" : f.TipoComprobante,
                        Numero = f.NumeroFormateado,
                        Tercero = f.Cliente,
                        Cuit = f.ClienteObj?.Cuit ?? f.ProveedorObj?.Cuit ?? f.SocioObj?.Dni,
                        CondicionIva = f.ClienteObj?.CondicionIva ?? f.ProveedorObj?.CondicionIva
                                       ?? (f.SocioObj != null ? "Consumidor Final" : null),
                        Neto = f.Subtotal,
                        Iva = f.Iva,
                        Total = f.Total,
                        Anulada = f.Anulada
                    }).ToList());
                }

                default:
                    return BadRequest("Tipo de reporte no válido.");
            }
        }

        private record MovimientoTesoreria(DateTime Fecha, string Detalle, decimal Ingreso, decimal Egreso);

        /// <summary>
        /// Libro Caja/Banco: saldo al inicio del período y saldo después de cada movimiento.
        /// </summary>
        private static object ConSaldos(List<MovimientoTesoreria> todos, DateTime? desde)
        {
            var anteriores = todos.Where(m => desde.HasValue && m.Fecha.Date < desde.Value.Date).ToList();
            var saldoInicial = anteriores.Sum(m => m.Ingreso - m.Egreso);
            var saldo = saldoInicial;
            var movimientos = todos.Skip(anteriores.Count).Select(m =>
            {
                saldo += m.Ingreso - m.Egreso;
                return new { fecha = m.Fecha, descripcion = m.Detalle, ingreso = m.Ingreso, egreso = m.Egreso, saldo };
            }).ToList();
            return new { saldoInicial, saldoFinal = saldo, movimientos };
        }
    }

    public class ReporteLibroIvaDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("fecha")]
        public DateTime Fecha { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("comprobante")]
        public string Comprobante { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("numero")]
        public string? Numero { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("tercero")]
        public string Tercero { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("cuit")]
        public string? Cuit { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("condicionIva")]
        public string? CondicionIva { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("neto")]
        public decimal Neto { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("iva")]
        public decimal Iva { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("total")]
        public decimal Total { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("anulada")]
        public bool Anulada { get; set; }
    }
}
