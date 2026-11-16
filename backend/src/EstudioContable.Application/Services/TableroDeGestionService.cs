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

    public interface ITableroDeGestionService
    {
        Task<IndicadoresDelMes> IndicadoresAsync(DateOnly primerDia, DateOnly hoy, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Cálculos del Tablero de Gestión. Todo sale de agrupaciones hechas por la base sobre las mismas tablas que el Resumen y los
    /// libros, así los números coinciden. Los días son de Argentina (UTC-3).
    /// </summary>
    public class TableroDeGestionService : ITableroDeGestionService
    {
        /// <summary>Un producto con este stock o menos es «poco stock» (el mismo umbral del Resumen).</summary>
        public const int StockBajo = 5;
        private static readonly TimeSpan Desfase = TimeSpan.FromHours(3);   // Argentina = UTC - 3

        private readonly IAppDbContext _context;

        public TableroDeGestionService(IAppDbContext context) => _context = context;

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
    }
}
