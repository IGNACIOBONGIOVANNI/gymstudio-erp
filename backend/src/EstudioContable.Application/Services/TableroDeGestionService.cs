using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Common;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain;
using EstudioContable.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Services
{
    /// <summary>Un indicador ya calculado: valor del mes, del mes anterior, variación, meta y semáforo.</summary>
    public record Indicador(string Clave, string Nombre, string Perspectiva, string Sentido, bool EsPorcentaje, bool AdmiteMeta,
        decimal? Valor, decimal? Anterior, decimal? Variacion, decimal? Meta, string Semaforo, string? Detalle);

    public record IndicadoresDelMes(string Mes, string MesAnterior, DateOnly Cierre, IReadOnlyList<Indicador> Indicadores);

    public record PuntoDeEvolucion(string Periodo, decimal Ventas, decimal Compras, decimal Cuotas, decimal Resultado);
    public record IngresoPorPlan(string Plan, decimal Neto, int Cobros);
    public record ProductoVendido(string Producto, decimal Cantidad, decimal Total);
    public record CompraPorProveedor(string Proveedor, decimal Neto);

    /// <summary>
    /// Análisis de un período: evolución (por mes, o por día si es un solo mes), ingresos por plan, mapa de asistencia
    /// (lunes a domingo × franjas de 06 a 22 h), productos más vendidos y en qué se gasta.
    /// </summary>
    public record Analisis(string Granularidad, IReadOnlyList<PuntoDeEvolucion> Evolucion, IReadOnlyList<IngresoPorPlan> IngresosPorPlan,
        int[][] Asistencia, IReadOnlyList<ProductoVendido> TopProductos, IReadOnlyList<CompraPorProveedor> ComprasPorProveedor);

    /// <summary>Un socio para contactar: «no-viene» (paga y no viene) o «por-vencer» (su cuota vence pronto), con la acción sugerida.</summary>
    public record SocioParaAccionar(long Id, string Nombre, string Apellido, string Telefono, string? Plan, string Motivo, int Dias, string Accion);

    /// <summary>Base del simulador: socios vigentes de cada plan y lo que recauda por mes (precio / duración × socios).</summary>
    public record PlanParaSimular(long Id, string Nombre, decimal PrecioMensual, int DuracionMeses, int SociosVigentes, decimal RecaudacionMensual);

    public record Decisiones(IReadOnlyList<SocioParaAccionar> Socios, IReadOnlyList<PlanParaSimular> Planes);

    public interface ITableroDeGestionService
    {
        Task<Decisiones> DecisionesAsync(DateOnly hoy, CancellationToken cancellationToken);
        Task<IndicadoresDelMes> IndicadoresAsync(DateOnly primerDia, DateOnly hoy, CancellationToken cancellationToken);
        Task<Analisis> AnalisisAsync(DateOnly desde, DateOnly hasta, long? planId, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Cálculos del Tablero de Gestión. Todo sale de agrupaciones hechas por la base sobre las mismas tablas que el Resumen y los
    /// libros, así los números coinciden. Los días son de Argentina (UTC-3).
    /// </summary>
    public class TableroDeGestionService : ITableroDeGestionService
    {
        /// <summary>Un producto con este stock o menos es «poco stock» (el mismo umbral del Resumen).</summary>
        public const int StockBajo = 5;
        public const int MesesMaximosDeAnalisis = 24;
        public const int PrimeraHora = 6;
        public const int UltimaHora = 22;
        /// <summary>Así empieza el ítem de la factura de una cuota; lo que sigue es el nombre del plan cuando se cobró.</summary>
        public const string PrefijoDeCuota = "Cuota Membresía - ";
        private static readonly TimeSpan Desfase = TimeSpan.FromHours(3);   // Argentina = UTC - 3

        /// <summary>Días hacia adelante para avisar que vence la cuota (los mismos del Resumen).</summary>
        public const int DiasPorVencer = 7;

        private readonly IAppDbContext _context;
        private readonly IAsistenciasService _asistencias;

        public TableroDeGestionService(IAppDbContext context, IAsistenciasService asistencias)
        {
            _context = context;
            _asistencias = asistencias;
        }

        /// <summary>Medianoche del día en la convención de las facturas (hora de Argentina guardada como UTC).</summary>
        private static DateTime Utc(DateOnly d) => new(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>El instante real (UTC) en que empieza ese día en Argentina: para membresías, altas y asistencias.</summary>
        private static DateTime InicioArgentina(DateOnly d) => Utc(d) + Desfase;

        private static decimal? Porcentaje(decimal parte, decimal total) => total == 0 ? null : Math.Round(parte / total * 100m, 2);

        public async Task<IndicadoresDelMes> IndicadoresAsync(DateOnly primerDia, DateOnly hoy, CancellationToken cancellationToken)
        {
            var anteriorPrimerDia = primerDia.AddMonths(-1);
            var actual = await MedirMesAsync(primerDia, hoy, cancellationToken);
            var anterior = await MedirMesAsync(anteriorPrimerDia, hoy, cancellationToken);
            var fotos = await FotosDeHoyAsync(hoy, cancellationToken);
            var metas = await _context.MetasDeIndicadores.AsNoTracking().ToDictionaryAsync(m => m.Clave, m => m.Valor, cancellationToken);

            var lista = TableroDeGestion.Indicadores.Select(d =>
            {
                var esFoto = fotos.Valores.ContainsKey(d.Clave);
                var valor = esFoto ? fotos.Valores[d.Clave] : actual.Valores[d.Clave];
                decimal? previo = esFoto ? null : anterior.Valores[d.Clave];
                decimal? meta = d.AdmiteMeta && metas.TryGetValue(d.Clave, out var m) ? m : null;
                string? detalle = d.Clave switch
                {
                    "asistencias" => actual.HorarioPico,
                    _ when esFoto => "Foto de hoy: no se compara con el mes anterior.",
                    _ => null
                };
                return new Indicador(d.Clave, d.Nombre, d.Perspectiva, d.Sentido == SentidoDelIndicador.MasEsMejor ? "mas" : "menos",
                    d.EsPorcentaje, d.AdmiteMeta, valor, previo, TableroDeGestion.Variacion(valor, previo, d.EsPorcentaje), meta,
                    TableroDeGestion.Semaforo(valor, meta, d.Sentido), detalle);
            }).ToList();

            return new IndicadoresDelMes(primerDia.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                anteriorPrimerDia.ToString("yyyy-MM", CultureInfo.InvariantCulture), actual.Cierre, lista);
        }

        private record Medicion(DateOnly Cierre, Dictionary<string, decimal?> Valores, string? HorarioPico);

        private record Fotos(Dictionary<string, decimal?> Valores);

        /// <summary>Socios activos con una membresía pagada vigente ese día (de Argentina).</summary>
        private IQueryable<long> VigentesEl(DateOnly dia)
        {
            var corte = InicioArgentina(dia.AddDays(1));
            return _context.Membresias.AsNoTracking()
                .Where(m => m.EstaPagada && m.FechaInicio < corte && m.FechaVencimiento >= corte && m.Socio.Activo)
                .Select(m => m.SocioId)
                .Distinct();
        }

        private async Task<Medicion> MedirMesAsync(DateOnly primerDia, DateOnly hoy, CancellationToken ct)
        {
            var finDelMes = primerDia.AddMonths(1).AddDays(-1);
            var cierre = hoy < finDelMes ? (hoy < primerDia ? primerDia : hoy) : finDelMes;
            var desde = Utc(primerDia);
            var hasta = Utc(primerDia.AddMonths(1));

            // Financiera: neto de ventas y compras del mes, sin anuladas (como el Resumen).
            var facturas = await _context.Facturas.AsNoTracking()
                .Where(f => !f.Anulada && f.Fecha >= desde && f.Fecha < hasta)
                .GroupBy(f => new { f.TipoOperacion, EsCuota = f.SocioId != null })
                .Select(g => new { g.Key.TipoOperacion, g.Key.EsCuota, Neto = g.Sum(f => f.Subtotal) })
                .ToListAsync(ct);
            var ventas = facturas.Where(x => x.TipoOperacion == "Venta").Sum(x => x.Neto);
            var compras = facturas.Where(x => x.TipoOperacion == "Compra").Sum(x => x.Neto);
            var cuotas = facturas.Where(x => x.TipoOperacion == "Venta" && x.EsCuota).Sum(x => x.Neto);
            var resultado = ventas - compras;

            // Socios: vigentes al empezar el mes y al cierre; bajas = los que estaban y ya no.
            var alInicio = await VigentesEl(primerDia).ToListAsync(ct);
            var alCierre = await VigentesEl(cierre).ToListAsync(ct);
            var bajas = alInicio.Except(alCierre).Count();
            var altas = await _context.Socios.AsNoTracking()
                .CountAsync(s => s.FechaAlta >= InicioArgentina(primerDia) && s.FechaAlta < InicioArgentina(primerDia.AddMonths(1)), ct);

            // Procesos: asistencias del mes y la franja de una hora con más ingresos.
            var porHora = await _context.AsistenciasSocio.AsNoTracking()
                .Where(a => a.Fecha >= primerDia && a.Fecha <= cierre)
                .GroupBy(a => a.RegistradaEn.Hour)
                .Select(g => new { HoraUtc = g.Key, Cantidad = g.Count() })
                .ToListAsync(ct);
            var asistencias = porHora.Sum(x => x.Cantidad);
            var pico = porHora.OrderByDescending(x => x.Cantidad).ThenBy(x => x.HoraUtc).FirstOrDefault();
            string? horarioPico = pico is null ? null
                : $"Horario pico: de {(pico.HoraUtc + 21) % 24:00} a {(pico.HoraUtc + 22) % 24:00} h ({pico.Cantidad} ingresos).";

            // Personas: horas cumplidas y su costo con el valor guardado en cada día.
            var horas = await _context.HorasDeEntrenador.AsNoTracking()
                .Where(h => h.Fecha >= primerDia && h.Fecha <= finDelMes)
                .GroupBy(h => 1)
                .Select(g => new { Horas = g.Sum(h => h.Horas), Costo = g.Sum(h => h.Horas * h.ValorHora) })
                .FirstOrDefaultAsync(ct);
            var costo = Math.Round(horas?.Costo ?? 0m, 2);

            var valores = new Dictionary<string, decimal?>
            {
                ["ingresos"] = ventas,
                ["resultado"] = resultado,
                ["margen"] = Porcentaje(resultado, ventas),
                ["socios-activos"] = alCierre.Count,
                ["altas"] = altas,
                ["bajas"] = bajas,
                ["retencion"] = Porcentaje(alInicio.Count - bajas, alInicio.Count),
                ["asistencias"] = asistencias,
                ["asistencia-por-socio"] = alCierre.Count == 0 ? null : Math.Round((decimal)asistencias / alCierre.Count, 2),
                ["horas-entrenadores"] = horas?.Horas ?? 0m,
                ["costo-entrenadores"] = costo,
                ["costo-sobre-cuotas"] = Porcentaje(costo, cuotas),
            };
            return new Medicion(cierre, valores, horarioPico);
        }

        /// <summary>Indicadores que son una foto del momento (no tienen mes): saldos, cuotas vencidas y stock.</summary>
        private async Task<Fotos> FotosDeHoyAsync(DateOnly hoy, CancellationToken ct)
        {
            var clienteId = PlanDeCuentas.ClienteIdPorDefecto.ToString();
            var caja = await _context.MovimientosCaja.AsNoTracking().Where(m => m.ClienteId == clienteId)
                .SumAsync(m => m.TipoMovimiento == TipoMovimientoCaja.Ingreso ? m.Importe : -m.Importe, ct);
            var banco = await _context.MovimientosBanco.AsNoTracking().Where(m => m.ClienteId == clienteId)
                .SumAsync(m => m.TipoMovimiento == TipoMovimientoBanco.Ingreso ? m.Importe : -m.Importe, ct);

            // Como el Resumen: vencida = tuvo plan y su vencimiento pasó (contra el inicio del día de hoy).
            var inicioHoy = Utc(hoy);
            var activos = _context.Socios.AsNoTracking().Where(s => s.Activo);
            var totalActivos = await activos.CountAsync(ct);
            var vencidos = await activos.CountAsync(s => s.FechaVencimiento != null && s.FechaVencimiento < inicioHoy, ct);
            var stockCritico = await _context.Productos.AsNoTracking().CountAsync(p => p.StockActual <= StockBajo, ct);

            return new Fotos(new Dictionary<string, decimal?>
            {
                ["liquidez"] = caja + banco,
                ["cuotas-vencidas"] = Porcentaje(vencidos, totalActivos),
                ["stock-critico"] = stockCritico,
            });
        }
    
        public async Task<Analisis> AnalisisAsync(DateOnly desde, DateOnly hasta, long? planId, CancellationToken ct)
        {
            if (hasta < desde)
                throw new ArgumentException("El mes «desde» no puede ser posterior al mes «hasta».");
            var meses = (hasta.Year - desde.Year) * 12 + hasta.Month - desde.Month + 1;
            if (meses > MesesMaximosDeAnalisis)
                throw new ArgumentException($"El período de análisis puede abarcar hasta {MesesMaximosDeAnalisis} meses.");
            string? nombreDelPlan = null;
            if (planId is not null)
            {
                nombreDelPlan = await _context.Planes.AsNoTracking().Where(p => p.Id == planId).Select(p => p.Nombre).FirstOrDefaultAsync(ct)
                    ?? throw new ArgumentException("El plan elegido no existe.");
            }

            var porDia = meses == 1;
            var ultimoDia = hasta.AddMonths(1).AddDays(-1);
            var inicio = Utc(desde);
            var fin = Utc(hasta.AddMonths(1));
            var facturas = _context.Facturas.AsNoTracking().Where(f => !f.Anulada && f.Fecha >= inicio && f.Fecha < fin);

            // Ventas y compras por día (después se suman por mes si hace falta).
            var diarias = await facturas
                .GroupBy(f => new { f.Fecha.Year, f.Fecha.Month, f.Fecha.Day, f.TipoOperacion, EsCuota = f.SocioId != null })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.TipoOperacion, g.Key.EsCuota, Neto = g.Sum(f => f.Subtotal) })
                .ToListAsync(ct);

            // Ítems de cuotas: dan el plan de cada cobro (el nombre que tenía al cobrarse).
            var cuotas = await (from i in _context.FacturaItems.AsNoTracking()
                                join f in facturas on i.FacturaId equals f.Id
                                where f.TipoOperacion == "Venta" && f.SocioId != null && i.Descripcion.StartsWith(PrefijoDeCuota)
                                group i by new { i.Descripcion, f.Fecha.Year, f.Fecha.Month, f.Fecha.Day } into g
                                select new { g.Key.Descripcion, g.Key.Year, g.Key.Month, g.Key.Day, Neto = g.Sum(x => x.Subtotal), Cobros = g.Count() })
                .ToListAsync(ct);
            var cuotasDelPlan = cuotas
                .Select(c => new { Plan = c.Descripcion.Substring(PrefijoDeCuota.Length).Trim(), c.Year, c.Month, c.Day, c.Neto, c.Cobros })
                .Where(c => nombreDelPlan is null || c.Plan == nombreDelPlan)
                .ToList();

            string Clave(int y, int m, int d) => porDia
                ? new DateOnly(y, m, d).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : new DateOnly(y, m, 1).ToString("yyyy-MM", CultureInfo.InvariantCulture);
            var periodos = porDia
                ? Enumerable.Range(0, ultimoDia.Day).Select(d => desde.AddDays(d).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).ToList()
                : Enumerable.Range(0, meses).Select(m => desde.AddMonths(m).ToString("yyyy-MM", CultureInfo.InvariantCulture)).ToList();
            var evolucion = periodos.Select(p =>
            {
                var delPeriodo = diarias.Where(x => Clave(x.Year, x.Month, x.Day) == p).ToList();
                var ventas = delPeriodo.Where(x => x.TipoOperacion == "Venta").Sum(x => x.Neto);
                var compras = delPeriodo.Where(x => x.TipoOperacion == "Compra").Sum(x => x.Neto);
                var deCuotas = nombreDelPlan is null
                    ? delPeriodo.Where(x => x.TipoOperacion == "Venta" && x.EsCuota).Sum(x => x.Neto)
                    : cuotasDelPlan.Where(x => Clave(x.Year, x.Month, x.Day) == p).Sum(x => x.Neto);
                return new PuntoDeEvolucion(p, ventas, compras, deCuotas, ventas - compras);
            }).ToList();

            var ingresosPorPlan = cuotasDelPlan.GroupBy(c => c.Plan)
                .Select(g => new IngresoPorPlan(g.Key, g.Sum(x => x.Neto), g.Sum(x => x.Cobros)))
                .OrderByDescending(x => x.Neto).ThenBy(x => x.Plan)
                .ToList();

            // Mapa de asistencia: día de la semana (de la fecha del ingreso) × hora de Argentina de cuando se registró.
            var asistencias = _context.AsistenciasSocio.AsNoTracking().Where(a => a.Fecha >= desde && a.Fecha <= ultimoDia);
            if (planId is not null)
                asistencias = asistencias.Where(a => _context.Socios.Any(s => s.Id == a.SocioId && s.PlanActualId == planId));
            var porHora = await asistencias.GroupBy(a => new { a.Fecha, Hora = a.RegistradaEn.Hour })
                .Select(g => new { g.Key.Fecha, g.Key.Hora, Cantidad = g.Count() })
                .ToListAsync(ct);
            var franjas = UltimaHora - PrimeraHora + 1;
            var mapa = Enumerable.Range(0, 7).Select(_ => new int[franjas]).ToArray();
            foreach (var x in porHora)
            {
                var hora = (x.Hora + 21) % 24;
                if (hora < PrimeraHora || hora > UltimaHora) continue;
                var dia = ((int)x.Fecha.DayOfWeek + 6) % 7;   // lunes = 0
                mapa[dia][hora - PrimeraHora] += x.Cantidad;
            }

            var top = await (from i in _context.FacturaItems.AsNoTracking()
                             join f in facturas on i.FacturaId equals f.Id
                             where f.TipoOperacion == "Venta" && i.ProductoId != null
                             group i by i.ProductoId into g
                             select new { ProductoId = g.Key!.Value, Cantidad = g.Sum(x => x.Cantidad), Total = g.Sum(x => x.Subtotal) })
                .OrderByDescending(x => x.Total).ThenBy(x => x.ProductoId)
                .Take(5)
                .ToListAsync(ct);
            var ids = top.Select(x => x.ProductoId).ToList();
            var nombres = await _context.Productos.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Nombre, ct);
            var topProductos = top.Select(x => new ProductoVendido(nombres.GetValueOrDefault(x.ProductoId) ?? "(producto eliminado)", x.Cantidad, x.Total)).ToList();

            var porProveedor = await facturas.Where(f => f.TipoOperacion == "Compra")
                .GroupBy(f => f.Cliente)
                .Select(g => new { Proveedor = g.Key, Neto = g.Sum(f => f.Subtotal) })
                .OrderByDescending(x => x.Neto).ThenBy(x => x.Proveedor)
                .Take(8)
                .ToListAsync(ct);

            return new Analisis(porDia ? "dia" : "mes", evolucion, ingresosPorPlan, mapa, topProductos,
                porProveedor.Select(x => new CompraPorProveedor(x.Proveedor, x.Neto)).ToList());
        }
    
        public async Task<Decisiones> DecisionesAsync(DateOnly hoy, CancellationToken ct)
        {
            var inicioHoy = Utc(hoy);
            // Primero los que pagan y no vienen (la regla de Recepción); después a quienes les vence la cuota, si no están ya.
            var enRiesgo = await _asistencias.SociosEnRiesgoAsync(hoy, ReglasDeAsistencia.DiasSinVenirPorDefecto, ct);
            var limite = Utc(hoy.AddDays(DiasPorVencer + 1));
            var porVencer = await _context.Socios.AsNoTracking()
                .Where(s => s.Activo && s.FechaVencimiento != null && s.FechaVencimiento >= inicioHoy && s.FechaVencimiento < limite)
                .Select(s => new { s.Id, s.Nombre, s.Apellido, s.Telefono, s.FechaVencimiento })
                .ToListAsync(ct);
            var ids = enRiesgo.Select(s => s.Id).Concat(porVencer.Select(s => s.Id)).Distinct().ToList();
            var planes = await _context.Socios.AsNoTracking().Where(s => ids.Contains(s.Id))
                .Select(s => new { s.Id, Plan = s.PlanActualObj != null ? s.PlanActualObj.Nombre : null })
                .ToDictionaryAsync(s => s.Id, s => s.Plan, ct);

            var socios = enRiesgo
                .Select(s => new SocioParaAccionar(s.Id, s.Nombre, s.Apellido, s.Telefono, planes.GetValueOrDefault(s.Id), "no-viene", s.DiasSinVenir,
                    $"Escribile para saber cómo está e invitarlo a volver: no viene hace {s.DiasSinVenir} días y tiene la cuota paga."))
                .Concat(porVencer.Where(s => enRiesgo.All(r => r.Id != s.Id))
                    .Select(s =>
                    {
                        var dias = DateOnly.FromDateTime(s.FechaVencimiento!.Value).DayNumber - hoy.DayNumber;
                        return new SocioParaAccionar(s.Id, s.Nombre, s.Apellido, s.Telefono, planes.GetValueOrDefault(s.Id), "por-vencer", dias,
                            dias == 0 ? "Recordale que su cuota vence hoy y ofrecele renovarla." : $"Recordale que su cuota vence en {dias} días y ofrecele renovarla.");
                    })
                    .OrderBy(s => s.Dias).ThenBy(s => s.Apellido))
                .ToList();

            var vigentesPorPlan = await _context.Socios.AsNoTracking()
                .Where(s => s.Activo && s.PlanActualId != null && s.FechaVencimiento != null && s.FechaVencimiento >= inicioHoy)
                .GroupBy(s => s.PlanActualId!.Value)
                .Select(g => new { PlanId = g.Key, Cantidad = g.Count() })
                .ToDictionaryAsync(x => x.PlanId, x => x.Cantidad, ct);
            var simulables = (await _context.Planes.AsNoTracking().Where(p => p.Activo).OrderBy(p => p.Nombre).ToListAsync(ct))
                .Select(p =>
                {
                    var vigentes = vigentesPorPlan.GetValueOrDefault(p.Id);
                    var meses = Math.Max(1, p.DuracionMeses);
                    return new PlanParaSimular(p.Id, p.Nombre, p.PrecioMensual, meses, vigentes, Math.Round(p.PrecioMensual / meses * vigentes, 2));
                })
                .ToList();

            return new Decisiones(socios, simulables);
        }
    }
}
