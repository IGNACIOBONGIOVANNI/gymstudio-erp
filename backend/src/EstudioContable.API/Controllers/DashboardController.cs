using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.API.Auth;
using EstudioContable.Application.Common;
using EstudioContable.Application.Interfaces;
using EstudioContable.Application.Services;
using EstudioContable.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.API.Controllers
{
    /// <summary>
    /// Resumen de la pantalla de inicio. Todo sale de agrupaciones hechas por la base y de las mismas
    /// tablas que los libros, así que los números coinciden con el Diario, el Mayor y la Caja.
    /// Los importes solo se devuelven a Administrador y Contador: Recepción recibe únicamente lo del gimnasio.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        /// <summary>Un producto con este stock o menos se avisa como "stock bajo".</summary>
        public const int StockBajo = 5;
        public const int DiasPorVencer = 7;
        private const int MesesDeSerie = 6;

        private readonly IAppDbContext _context;
        private readonly ICuentasCorrientesService _cuentas;
        private readonly IControlIntegridadService _integridad;

        public DashboardController(IAppDbContext context, ICuentasCorrientesService cuentas, IControlIntegridadService integridad)
        {
            _context = context;
            _cuentas = cuentas;
            _integridad = integridad;
        }

        /// <summary>Una novedad del resumen: nivel "atencion", "info" u "ok" y su texto.</summary>
        public record Novedad(string Nivel, string Texto);

        private record FinanzasParaNovedades(decimal Ventas, decimal VentasMesAnterior, string MesAnterior, decimal Compras, decimal Caja);
        private record ResumenGimnasio(int SociosConCuotaAlDia, int SociosVencidos, int SociosSinPlan, int CuotasPorVencer, int DiasPorVencer, int SociosSinRutina, int RutinasVencidas);
        private record ProductoConPocoStock(long Id, string Nombre, int Stock);

        /// <param name="mes">Mes a resumir ("2026-10"); por defecto el de <paramref name="hoy"/>.</param>
        /// <param name="hoy">Día de hoy del navegador ("2026-10-27"); por defecto el de Argentina (UTC-3).</param>
        [HttpGet]
        public async Task<IActionResult> Resumen([FromQuery] string? mes, [FromQuery] DateOnly? hoy, CancellationToken cancellationToken)
        {
            var dia = hoy ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));
            if (dia.Year < 2000 || dia.Year > 2100) throw new ArgumentException("La fecha de hoy debe estar entre los años 2000 y 2100.");
            var primerDia = ParsearMes(mes) ?? new DateOnly(dia.Year, dia.Month, 1);

            var gimnasio = await ResumenGimnasioAsync(dia, cancellationToken);
            var stockBajo = await _context.Productos.AsNoTracking()
                .Where(p => p.StockActual <= StockBajo)
                .OrderBy(p => p.StockActual).ThenBy(p => p.Nombre)
                .Take(8)
                .Select(p => new ProductoConPocoStock(p.Id, p.Nombre, p.StockActual))
                .ToListAsync(cancellationToken);

            var veFinanzas = User.IsInRole(Roles.Administrador) || User.IsInRole(Roles.Contador);
            (object Json, FinanzasParaNovedades Datos)? finanzas = veFinanzas ? await ResumenFinanzasAsync(primerDia, cancellationToken) : null;
            var novedades = await NovedadesAsync(veFinanzas, dia, primerDia, finanzas?.Datos, gimnasio, stockBajo, cancellationToken);
            return Ok(new
            {
                Mes = primerDia.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                Hoy = dia,
                Finanzas = finanzas?.Json,
                Gimnasio = gimnasio,
                StockBajo = stockBajo.Select(p => new { p.Id, p.Nombre, p.Stock }),
                UmbralStockBajo = StockBajo,
                Novedades = novedades
            });
        }

        private static readonly string[] Meses = { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };

        private static string NombreDelMes(string yyyyMm)
        {
            var partes = yyyyMm.Split('-');
            return $"{Meses[int.Parse(partes[1], CultureInfo.InvariantCulture) - 1]} de {partes[0]}";
        }

        /// <summary>Importe con el formato argentino: $ 1.234.567,89.</summary>
        private static string Pesos(decimal v)
            => "$ " + Math.Abs(v).ToString("N2", CultureInfo.InvariantCulture).Replace(",", "#").Replace(".", ",").Replace("#", ".");

        /// <summary>
        /// Frases automáticas sobre cómo viene el negocio, calculadas con los propios datos (sin servicios externos).
        /// Primero lo que requiere atención. Lo financiero solo se calcula para Administrador y Contador.
        /// </summary>
        private async Task<List<Novedad>> NovedadesAsync(bool veFinanzas, DateOnly dia, DateOnly primerDia, FinanzasParaNovedades? f,
            ResumenGimnasio g, List<ProductoConPocoStock> stockBajo, CancellationToken cancellationToken)
        {
            var lista = new List<Novedad>();
            string Plural(int n, string uno, string varios) => n == 1 ? uno : varios;

            if (veFinanzas && f is not null)
            {
                var mes = NombreDelMes(primerDia.ToString("yyyy-MM", CultureInfo.InvariantCulture));
                if (f.VentasMesAnterior > 0)
                {
                    var porcentaje = Math.Round((f.Ventas - f.VentasMesAnterior) / f.VentasMesAnterior * 100m, 0);
                    lista.Add(new("info", Math.Abs(porcentaje) < 10
                        ? $"Las ventas de {mes} se mantienen parecidas a las de {NombreDelMes(f.MesAnterior)} ({(porcentaje >= 0 ? "+" : "-")}{Math.Abs(porcentaje)} %)."
                        : $"Las ventas de {mes} {(porcentaje > 0 ? "subieron" : "bajaron")} un {Math.Abs(porcentaje)} % respecto de {NombreDelMes(f.MesAnterior)}."));
                }
                else if (f.Ventas > 0)
                    lista.Add(new("info", $"En {mes} ya se vendieron {Pesos(f.Ventas)} (netos); el mes anterior no hubo ventas."));
                else
                    lista.Add(new("info", $"Todavía no hay ventas cargadas en {mes}."));

                if (f.Compras > f.Ventas)
                    lista.Add(new("atencion", $"En {mes} se compró más de lo que se vendió: el resultado del mes es de -{Pesos(f.Compras - f.Ventas)}."));
                if (f.Caja < 0)
                    lista.Add(new("atencion", $"La Caja está en negativo (-{Pesos(f.Caja)}): revisá los movimientos de caja."));

                var clientes = await _cuentas.ListadoAsync(true, dia, cancellationToken);
                var viejas = clientes.Antiguedad.Mas90;
                var masDe30 = clientes.Antiguedad.De31A60 + clientes.Antiguedad.De61A90 + viejas;
                if (viejas > 0)
                {
                    var peor = clientes.Terceros.OrderByDescending(t => t.Antiguedad.Mas90).First();
                    lista.Add(new("atencion", $"Los clientes tienen {Pesos(viejas)} con más de 90 días de antigüedad; la deuda más vieja es de {peor.Nombre} ({Pesos(peor.Antiguedad.Mas90)})."));
                }
                else if (masDe30 > 0)
                    lista.Add(new("info", $"Hay {Pesos(masDe30)} de deuda de clientes con más de 30 días: conviene reclamarla."));
                else if (clientes.Saldo > 0)
                    lista.Add(new("info", $"Los clientes te deben {Pesos(clientes.Saldo)}, todo con menos de 30 días."));

                var proveedores = await _cuentas.ListadoAsync(false, dia, cancellationToken);
                var proveedoresViejas = proveedores.Antiguedad.De61A90 + proveedores.Antiguedad.Mas90;
                if (proveedoresViejas > 0)
                    lista.Add(new("atencion", $"Se le debe {Pesos(proveedores.Saldo)} a proveedores, de los cuales {Pesos(proveedoresViejas)} tienen más de 60 días."));
                else if (proveedores.Saldo > 0)
                    lista.Add(new("info", $"Se le debe {Pesos(proveedores.Saldo)} a proveedores, todo con menos de 60 días."));

                var integridad = await _integridad.EjecutarAsync(cancellationToken);
                lista.Add(integridad.Correctos == integridad.Total
                    ? new("ok", $"Contabilidad verificada: {integridad.Total} controles correctos (partida doble, Caja y Banco contra el Mayor, IVA, cuentas corrientes y stock).")
                    : new("atencion", $"El control de integridad encontró diferencias en {integridad.Total - integridad.Correctos} de {integridad.Total} controles: revisalo en Control de integridad."));
            }

            if (g.CuotasPorVencer > 0)
                lista.Add(new("info", $"{g.CuotasPorVencer} {Plural(g.CuotasPorVencer, "cuota vence", "cuotas vencen")} en los próximos {g.DiasPorVencer} días: avisales por WhatsApp desde Recepción."));
            if (g.SociosVencidos > 0)
                lista.Add(new("info", $"{g.SociosVencidos} {Plural(g.SociosVencidos, "socio tiene", "socios tienen")} la cuota vencida."));
            if (g.SociosSinRutina > 0)
                lista.Add(new("info", $"{g.SociosSinRutina} {Plural(g.SociosSinRutina, "socio al día no tiene", "socios al día no tienen")} rutina asignada."));
            if (g.RutinasVencidas > 0)
                lista.Add(new("atencion", $"{g.RutinasVencidas} {Plural(g.RutinasVencidas, "rutina vencida", "rutinas vencidas")}: hay que renovarla{(g.RutinasVencidas == 1 ? "" : "s")}."));

            var sinStock = stockBajo.Where(p => p.Stock <= 0).ToList();
            if (sinStock.Count > 0)
                lista.Add(new("atencion", $"{sinStock.Count} {Plural(sinStock.Count, "producto sin stock", "productos sin stock")}: {string.Join(", ", sinStock.Take(3).Select(p => p.Nombre))}{(sinStock.Count > 3 ? " y otros" : "")}."));
            var pocoStock = stockBajo.Count - sinStock.Count;
            if (pocoStock > 0)
                lista.Add(new("info", $"{pocoStock} {Plural(pocoStock, "producto con poco stock", "productos con poco stock")} (hasta {StockBajo} unidades)."));

            if (lista.Count == 0)
                lista.Add(new("ok", "Todo en orden: no hay alertas por ahora."));
            return lista.OrderBy(n => n.Nivel switch { "atencion" => 0, "info" => 1, _ => 2 }).ToList();
        }

        private static DateOnly? ParsearMes(string? mes)
        {
            if (string.IsNullOrWhiteSpace(mes)) return null;
            if (!DateOnly.TryParseExact(mes.Trim() + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
                throw new ArgumentException("El mes debe tener el formato aaaa-mm (por ejemplo 2026-10).");
            if (fecha.Year < 2000 || fecha.Year > 2100) throw new ArgumentException("El mes debe estar entre los años 2000 y 2100.");
            return fecha;
        }

        private static DateTime Utc(DateOnly d) => new(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc);

        private async Task<(object Json, FinanzasParaNovedades Datos)> ResumenFinanzasAsync(DateOnly primerDia, CancellationToken cancellationToken)
        {
            var inicioMes = Utc(primerDia);
            var finMes = inicioMes.AddMonths(1);
            var inicioSerie = inicioMes.AddMonths(-(MesesDeSerie - 1));

            // Una sola agrupación por mes y tipo de operación, sin las facturas anuladas.
            var porMes = await _context.Facturas.AsNoTracking()
                .Where(f => !f.Anulada && f.Fecha >= inicioSerie && f.Fecha < finMes)
                .GroupBy(f => new { f.Fecha.Year, f.Fecha.Month, f.TipoOperacion })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    g.Key.TipoOperacion,
                    Neto = g.Sum(f => f.Subtotal),
                    Iva = g.Sum(f => f.Iva),
                    Total = g.Sum(f => f.Total),
                    Cantidad = g.Count()
                })
                .ToListAsync(cancellationToken);

            var serie = Enumerable.Range(0, MesesDeSerie).Select(i =>
            {
                var m = inicioMes.AddMonths(i - (MesesDeSerie - 1));
                decimal Neto(string tipo) => porMes.Where(x => x.Year == m.Year && x.Month == m.Month && x.TipoOperacion == tipo).Sum(x => x.Neto);
                return new { Mes = m.ToString("yyyy-MM", CultureInfo.InvariantCulture), Ventas = Neto("Venta"), Compras = Neto("Compra") };
            }).ToList();

            object DelMes(string tipo)
            {
                var filas = porMes.Where(x => x.Year == inicioMes.Year && x.Month == inicioMes.Month && x.TipoOperacion == tipo).ToList();
                return new { Neto = filas.Sum(x => x.Neto), Iva = filas.Sum(x => x.Iva), Total = filas.Sum(x => x.Total), Cantidad = filas.Sum(x => x.Cantidad) };
            }
            var ventas = serie[^1].Ventas;
            var compras = serie[^1].Compras;
            var ivaVentas = porMes.Where(x => x.Year == inicioMes.Year && x.Month == inicioMes.Month && x.TipoOperacion == "Venta").Sum(x => x.Iva);
            var ivaCompras = porMes.Where(x => x.Year == inicioMes.Year && x.Month == inicioMes.Month && x.TipoOperacion == "Compra").Sum(x => x.Iva);

            var enMes = _context.Facturas.AsNoTracking().Where(f => f.Fecha >= inicioMes && f.Fecha < finMes);
            var anuladas = await enMes.CountAsync(f => f.Anulada, cancellationToken);

            // Ingresos del mes (neto de ventas): cuotas de socios (la factura lleva socio) y el resto.
            var origen = await enMes.Where(f => !f.Anulada && f.TipoOperacion == "Venta")
                .GroupBy(f => f.SocioId != null)
                .Select(g => new { EsCuota = g.Key, Neto = g.Sum(f => f.Subtotal) })
                .ToListAsync(cancellationToken);

            var topBase = await (from i in _context.FacturaItems.AsNoTracking()
                                 join f in enMes on i.FacturaId equals f.Id
                                 where !f.Anulada && f.TipoOperacion == "Venta" && i.ProductoId != null
                                 group i by i.ProductoId into g
                                 select new { ProductoId = g.Key!.Value, Cantidad = g.Sum(x => x.Cantidad), Total = g.Sum(x => x.Subtotal) })
                .OrderByDescending(x => x.Total).ThenBy(x => x.ProductoId)
                .Take(5)
                .ToListAsync(cancellationToken);
            var ids = topBase.Select(x => x.ProductoId).ToList();
            var nombres = await _context.Productos.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Nombre, cancellationToken);

            var clienteId = PlanDeCuentas.ClienteIdPorDefecto.ToString();
            var cajaSaldo = await _context.MovimientosCaja.AsNoTracking().Where(m => m.ClienteId == clienteId)
                .SumAsync(m => m.TipoMovimiento == TipoMovimientoCaja.Ingreso ? m.Importe : -m.Importe, cancellationToken);
            var bancoSaldo = await _context.MovimientosBanco.AsNoTracking().Where(m => m.ClienteId == clienteId)
                .SumAsync(m => m.TipoMovimiento == TipoMovimientoBanco.Ingreso ? m.Importe : -m.Importe, cancellationToken);

            // Lo que deben los clientes y lo que se debe a los proveedores: saldos del Mayor, que son los
            // mismos totales de la pantalla de cuentas a cobrar y a pagar.
            var cuentasPorCobrarYPagar = await _context.LibroMayor
                .Where(m => m.ClienteId == PlanDeCuentas.ClienteIdPorDefecto && (m.CuentaId == PlanDeCuentas.DeudoresPorVentas || m.CuentaId == PlanDeCuentas.Proveedores))
                .ToListAsync(cancellationToken);
            var deudores = cuentasPorCobrarYPagar.FirstOrDefault(m => m.CuentaId == PlanDeCuentas.DeudoresPorVentas);
            var proveedores = cuentasPorCobrarYPagar.FirstOrDefault(m => m.CuentaId == PlanDeCuentas.Proveedores);

            var json = new
            {
                ACobrar = deudores is null ? 0m : deudores.TotalDebe - deudores.TotalHaber,
                APagar = proveedores is null ? 0m : proveedores.TotalHaber - proveedores.TotalDebe,
                Ventas = DelMes("Venta"),
                Compras = DelMes("Compra"),
                Resultado = ventas - compras,                 // neto vendido menos neto comprado del mes
                IvaAPagar = ivaVentas - ivaCompras,           // débito fiscal menos crédito fiscal (negativo = a favor)
                CajaSaldo = cajaSaldo,
                BancoSaldo = bancoSaldo,
                FacturasAnuladas = anuladas,
                Serie = serie,
                IngresosPorOrigen = new[]
                {
                    new { Origen = "Cuotas de socios", Neto = origen.Where(x => x.EsCuota).Sum(x => x.Neto) },
                    new { Origen = "Productos y otros", Neto = origen.Where(x => !x.EsCuota).Sum(x => x.Neto) }
                },
                TopProductos = topBase.Select(x => new { Producto = nombres.GetValueOrDefault(x.ProductoId) ?? "(producto eliminado)", x.Cantidad, x.Total })
            };
            return (json, new FinanzasParaNovedades(ventas, serie[^2].Ventas, serie[^2].Mes, compras, cajaSaldo));
        }

        private async Task<ResumenGimnasio> ResumenGimnasioAsync(DateOnly hoy, CancellationToken cancellationToken)
        {
            var inicioHoy = Utc(hoy);
            var activos = _context.Socios.AsNoTracking().Where(s => s.Activo);
            var vigentes = activos.Where(s => s.FechaVencimiento >= inicioHoy);

            var conCuota = await vigentes.CountAsync(cancellationToken);
            // Vencida: tuvo plan y su vencimiento pasó. Sin plan: todavía no pagó ninguna cuota (como en Recepción).
            var vencidos = await activos.CountAsync(s => s.FechaVencimiento != null && s.FechaVencimiento < inicioHoy, cancellationToken);
            var sinPlan = await activos.CountAsync(s => s.FechaVencimiento == null, cancellationToken);
            var porVencer = await vigentes.CountAsync(s => s.FechaVencimiento < inicioHoy.AddDays(DiasPorVencer + 1), cancellationToken);
            var sinRutina = await vigentes.CountAsync(s => !_context.AsignacionesRutina.Any(a => a.SocioId == s.Id && a.Activa), cancellationToken);
            var rutinasVencidas = await _context.AsignacionesRutina.AsNoTracking()
                .CountAsync(a => a.Activa && a.FechaFin != null && a.FechaFin < hoy, cancellationToken);

            return new ResumenGimnasio(conCuota, vencidos, sinPlan, porVencer, DiasPorVencer, sinRutina, rutinasVencidas);
        }
    }
}
