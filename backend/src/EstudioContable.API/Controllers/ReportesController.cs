using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EstudioContable.Application.Interfaces;
using System.Linq;

namespace EstudioContable.API.Controllers
{
    [ApiController]
    [Route("api/reportes")]
    [AllowAnonymous] // Temporal para probar
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

            switch (tipo.ToLower())
            {
                case "diario":
                {
                    var queryDiario = _context.AsientosContables
                        .Include(a => a.Lineas)
                        .ThenInclude(l => l.Cuenta)
                        .AsNoTracking()
                        .AsQueryable();
                    if (desde.HasValue) queryDiario = queryDiario.Where(x => x.Fecha >= DateOnly.FromDateTime(desde.Value));
                    if (hasta.HasValue) queryDiario = queryDiario.Where(x => x.Fecha <= DateOnly.FromDateTime(hasta.Value));

                    var asientos = await queryDiario.OrderBy(x => x.Fecha).ThenBy(x => x.NumeroAsiento).ToListAsync(cancellationToken);
                    return Ok(asientos.Select(a => new {
                        id = a.Id,
                        numeroAsiento = a.NumeroAsiento,
                        fecha = a.Fecha,
                        descripcion = a.Descripcion,
                        totalDebe = a.Lineas.Sum(l => l.Debe),
                        totalHaber = a.Lineas.Sum(l => l.Haber),
                        debe = a.Lineas.Sum(l => l.Debe),
                        haber = a.Lineas.Sum(l => l.Haber),
                        lineas = a.Lineas.OrderBy(l => l.Orden).Select(l => new {
                            id = l.Id,
                            cuentaId = l.CuentaId,
                            nombreCuenta = l.Cuenta != null ? l.Cuenta.Nombre : $"Cuenta {l.CuentaId}",
                            debe = l.Debe,
                            haber = l.Haber,
                            descripcion = l.Descripcion,
                            orden = l.Orden
                        }).ToList()
                    }));
                }
                
                case "caja":
                {
                    var queryCaja = _context.MovimientosCaja.AsNoTracking().AsQueryable();
                    if (desde.HasValue) queryCaja = queryCaja.Where(x => x.Fecha.Date >= desde.Value.Date);
                    if (hasta.HasValue) queryCaja = queryCaja.Where(x => x.Fecha.Date <= hasta.Value.Date);

                    var movimientos = await queryCaja.OrderBy(x => x.Fecha).ToListAsync(cancellationToken);
                    return Ok(movimientos.Select(m => new {
                        fecha = m.Fecha,
                        descripcion = m.Detalle,
                        ingreso = m.TipoMovimiento == EstudioContable.Domain.Enums.TipoMovimientoCaja.Ingreso ? m.Importe : 0,
                        egreso = m.TipoMovimiento == EstudioContable.Domain.Enums.TipoMovimientoCaja.Egreso ? m.Importe : 0
                    }));
                }

                case "banco":
                {
                    var queryBanco = _context.MovimientosBanco.AsNoTracking().AsQueryable();
                    if (desde.HasValue) queryBanco = queryBanco.Where(x => x.Fecha.Date >= desde.Value.Date);
                    if (hasta.HasValue) queryBanco = queryBanco.Where(x => x.Fecha.Date <= hasta.Value.Date);

                    var movimientos = await queryBanco.OrderBy(x => x.Fecha).ToListAsync(cancellationToken);
                    return Ok(movimientos.Select(m => new {
                        fecha = m.Fecha,
                        descripcion = m.Detalle,
                        ingreso = m.TipoMovimiento == EstudioContable.Domain.Enums.TipoMovimientoBanco.Ingreso ? m.Importe : 0,
                        egreso = m.TipoMovimiento == EstudioContable.Domain.Enums.TipoMovimientoBanco.Egreso ? m.Importe : 0
                    }));
                }

                case "mayor":
                {
                    var queryLineas = _context.LineasAsiento
                        .Include(l => l.Cuenta)
                        .Include(l => l.Asiento)
                        .AsNoTracking()
                        .AsQueryable();

                    if (desde.HasValue) queryLineas = queryLineas.Where(x => x.Asiento.Fecha >= DateOnly.FromDateTime(desde.Value));
                    if (hasta.HasValue) queryLineas = queryLineas.Where(x => x.Asiento.Fecha <= DateOnly.FromDateTime(hasta.Value));

                    var lineas = await queryLineas.ToListAsync(cancellationToken);
                    var mayor = lineas.GroupBy(l => l.Cuenta.Nombre)
                        .Select(g => new {
                            cuenta = g.Key,
                            saldo = g.Sum(l => l.Debe) - g.Sum(l => l.Haber),
                            movimientos = g.Select(l => new {
                                fecha = l.Asiento.Fecha,
                                descripcion = l.Asiento.Descripcion,
                                debe = l.Debe,
                                haber = l.Haber
                            }).OrderBy(m => m.fecha).ToList()
                        }).ToList();

                    return Ok(mayor);
                }

                case "balance":
                {
                    var queryLineas = _context.LineasAsiento
                        .Include(l => l.Cuenta)
                        .Include(l => l.Asiento)
                        .AsNoTracking()
                        .AsQueryable();

                    if (desde.HasValue) queryLineas = queryLineas.Where(x => x.Asiento.Fecha >= DateOnly.FromDateTime(desde.Value));
                    if (hasta.HasValue) queryLineas = queryLineas.Where(x => x.Asiento.Fecha <= DateOnly.FromDateTime(hasta.Value));

                    var lineas = await queryLineas.ToListAsync(cancellationToken);
                    var balance = lineas
                        .Select(l => new {
                            NatReal = (l.Cuenta.Nombre.Contains("Caja") || l.Cuenta.Nombre.Contains("Banco")) 
                                ? EstudioContable.Domain.Entities.NaturalezaCuenta.Activo 
                                : l.Cuenta.Naturaleza,
                            l.Cuenta.Nombre,
                            l.Debe,
                            l.Haber
                        })
                        .GroupBy(x => new { x.NatReal, x.Nombre })
                        .Select(g => {
                            var debe = g.Sum(l => l.Debe);
                            var haber = g.Sum(l => l.Haber);
                            var saldoBruto = debe - haber; // Positivo si Debe > Haber

                            var esResultadoNegativo = g.Key.NatReal == EstudioContable.Domain.Entities.NaturalezaCuenta.Resultado && saldoBruto > 0;
                            var esResultadoPositivo = g.Key.NatReal == EstudioContable.Domain.Entities.NaturalezaCuenta.Resultado && saldoBruto < 0;

                            // Formula: Activo y Resultado Negativo -> Debe - Haber. Pasivo, PN, Resultado Positivo -> Haber - Debe
                            var saldo = (g.Key.NatReal == EstudioContable.Domain.Entities.NaturalezaCuenta.Activo || esResultadoNegativo)
                                ? saldoBruto
                                : -saldoBruto;

                            string natStr = g.Key.NatReal.ToString();
                            if (esResultadoNegativo) natStr = "Resultado Negativo";
                            if (esResultadoPositivo) natStr = "Resultado Positivo";

                            return new {
                                naturaleza = natStr,
                                cuenta = g.Key.Nombre,
                                saldo = saldo
                            };
                        }).Where(c => c.saldo != 0).OrderBy(c => c.naturaleza).ThenBy(c => c.cuenta).ToList();

                    return Ok(balance);
                }

                case "venta":
                {
                    var queryVentas = _context.Facturas.Where(f => f.TipoOperacion == "Venta").AsNoTracking().AsQueryable();
                    if (desde.HasValue) queryVentas = queryVentas.Where(x => x.Fecha.Date >= desde.Value.Date);
                    if (hasta.HasValue) queryVentas = queryVentas.Where(x => x.Fecha.Date <= hasta.Value.Date);

                    var ventas = await queryVentas.OrderBy(x => x.Fecha).ToListAsync(cancellationToken);
                    
                    var resultados = ventas.Select(v => new ReporteVentaDto {
                        Fecha = v.Fecha,
                        Comprobante = string.IsNullOrEmpty(v.TipoComprobante) ? "Factura" : v.TipoComprobante,
                        Cliente = v.Cliente,
                        Neto = v.Subtotal,
                        Iva = v.Iva,
                        Total = v.Total
                    }).ToList();

                    return Ok(resultados);
                }

                case "compra":
                {
                    var queryCompras = _context.Facturas.Where(f => f.TipoOperacion == "Compra").AsNoTracking().AsQueryable();
                    if (desde.HasValue) queryCompras = queryCompras.Where(x => x.Fecha.Date >= desde.Value.Date);
                    if (hasta.HasValue) queryCompras = queryCompras.Where(x => x.Fecha.Date <= hasta.Value.Date);

                    var compras = await queryCompras.OrderBy(x => x.Fecha).ToListAsync(cancellationToken);
                    
                    var resultados = compras.Select(c => new ReporteCompraDto {
                        Fecha = c.Fecha,
                        Comprobante = string.IsNullOrEmpty(c.TipoComprobante) ? "Factura" : c.TipoComprobante,
                        Proveedor = c.Cliente, // Suponiendo que en Compras, el campo Cliente guarda el Proveedor
                        Neto = c.Subtotal,
                        Iva = c.Iva,
                        Total = c.Total
                    }).ToList();

                    return Ok(resultados);
                }

                default:
                    return BadRequest("Tipo de reporte no válido.");
            }
        }
    }

    public class ReporteVentaDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("fecha")]
        public DateTime Fecha { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("comprobante")]
        public string Comprobante { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("cliente")]
        public string Cliente { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("neto")]
        public decimal Neto { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("iva")]
        public decimal Iva { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("total")]
        public decimal Total { get; set; }
    }

    public class ReporteCompraDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("fecha")]
        public DateTime Fecha { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("comprobante")]
        public string Comprobante { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("proveedor")]
        public string Proveedor { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("neto")]
        public decimal Neto { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("iva")]
        public decimal Iva { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("total")]
        public decimal Total { get; set; }
    }
}
