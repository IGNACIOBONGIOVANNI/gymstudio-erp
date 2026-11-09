using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Entrenadores;
using EstudioContable.Domain;
using EstudioContable.Domain.Entities;
using EstudioContable.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Infrastructure.Entrenadores
{
    /// <inheritdoc />
    /// <remarks>
    /// El «hoy» es el de Argentina (UTC-3) y sale del <see cref="TimeProvider"/>, así las pruebas lo fijan. Cada operación valida todo antes de
    /// guardar y guarda con un solo <c>SaveChanges</c>: o entra entera o no entra nada. Las horas programadas salen de los turnos del calendario
    /// (<see cref="ReglasDeHoras.Programadas"/>). La auditoría de cada fila la hace el disparador de la base.
    /// </remarks>
    public sealed class HorasDeEntrenadoresService(AppDbContext context, TimeProvider reloj) : IHorasDeEntrenadoresService
    {
        private static readonly StringComparer Alfabetico = StringComparer.Create(new CultureInfo("es-AR"), ignoreCase: true);

        private DateOnly Hoy() => ReglasDeHoras.HoyEnArgentina(reloj.GetUtcNow());

        private static IEnumerable<DateOnly> Dias(DateOnly desde, DateOnly hasta)
        {
            for (var dia = desde; dia <= hasta; dia = dia.AddDays(1)) yield return dia;
        }

        private async Task<List<(long EntrenadorId, int DiaSemana)>> TurnosAsync(CancellationToken cancellationToken)
        {
            var turnos = await context.TurnosEncargado.AsNoTracking().Select(t => new { t.EntrenadorId, t.DiaSemana }).ToListAsync(cancellationToken);
            return turnos.Select(t => (t.EntrenadorId, t.DiaSemana)).ToList();
        }

        public async Task<HorasDelPeriodo> ObtenerAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken)
        {
            ReglasDeHoras.ValidarPeriodo(desde, hasta);

            var registros = await context.HorasDeEntrenador.AsNoTracking().Where(h => h.Fecha >= desde && h.Fecha <= hasta).ToListAsync(cancellationToken);
            var conRegistros = registros.Select(h => h.EntrenadorId).Distinct().ToArray();
            // Los activos, y los desactivados que tienen horas en el período (sus horas se siguen viendo y corrigiendo).
            var entrenadores = await context.Entrenadores.AsNoTracking()
                .Where(e => e.Activo || conRegistros.Contains(e.Id))
                .Select(e => new EntrenadorDeHoras(e.Id, e.NombreCompleto, e.ValorHora, e.Activo))
                .ToListAsync(cancellationToken);
            // Alfabético, con el mismo criterio que el presupuesto (no el de la base de datos): así el orden es igual en las dos pantallas.
            entrenadores = entrenadores.OrderBy(e => e.Nombre, Alfabetico).ToList();

            var turnos = await TurnosAsync(cancellationToken);
            var porDia = registros.ToDictionary(h => (h.EntrenadorId, h.Fecha));
            var dias = new List<DiaDeHoras>();
            foreach (var entrenador in entrenadores)
                foreach (var fecha in Dias(desde, hasta))
                {
                    var programadas = ReglasDeHoras.Programadas(turnos, entrenador.Id, fecha);
                    porDia.TryGetValue((entrenador.Id, fecha), out var registro);
                    if (programadas > 0 || registro != null)
                        dias.Add(new DiaDeHoras(entrenador.Id, fecha, programadas, registro?.Horas, registro?.ValorHora, registro?.Nota));
                }

            return new HorasDelPeriodo(entrenadores, dias);
        }

        public async Task GuardarAsync(IReadOnlyList<CambioDeHoras> cambios, CancellationToken cancellationToken)
        {
            if (cambios is null) throw new ArgumentException("Faltan los días a guardar.");
            if (cambios.Count > ReglasDeHoras.RenglonesMaximos)
                throw new ArgumentException($"Se pueden guardar hasta {ReglasDeHoras.RenglonesMaximos} días por vez.");
            if (cambios.Count == 0) return;

            var hoy = Hoy();
            var vistos = new HashSet<(long, DateOnly)>();
            foreach (var cambio in cambios)
            {
                ReglasDeHoras.ValidarFecha(cambio.Fecha, hoy);
                if (cambio.Horas is { } horas) ReglasDeHoras.ValidarHoras(horas);
                ReglasDeHoras.LimpiarNota(cambio.Nota);
                if (!vistos.Add((cambio.EntrenadorId, cambio.Fecha)))
                    throw new ArgumentException("Hay un día repetido en el pedido: el mismo entrenador y la misma fecha.");
            }

            var ids = cambios.Select(c => c.EntrenadorId).Distinct().ToArray();
            var entrenadores = await context.Entrenadores.Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, cancellationToken);
            if (ids.Any(id => !entrenadores.ContainsKey(id))) throw new ArgumentException("Uno de los entrenadores ya no existe. Actualizá la pantalla.");

            var primero = cambios.Min(c => c.Fecha);
            var ultimo = cambios.Max(c => c.Fecha);
            var existentes = (await context.HorasDeEntrenador
                    .Where(h => ids.Contains(h.EntrenadorId) && h.Fecha >= primero && h.Fecha <= ultimo)
                    .ToListAsync(cancellationToken))
                .ToDictionary(h => (h.EntrenadorId, h.Fecha));

            // Todo se arma en memoria y se guarda junto al final: un renglón inválido no deja a los anteriores a medias.
            foreach (var cambio in cambios)
            {
                var entrenador = entrenadores[cambio.EntrenadorId];
                if (existentes.TryGetValue((cambio.EntrenadorId, cambio.Fecha), out var dia))
                {
                    // El día ya existe: se corrigen las horas y la nota (el valor de la hora que tenía no cambia) o se borra.
                    if (cambio.Horas is null) context.HorasDeEntrenador.Remove(dia);
                    else dia.Cambiar(cambio.Horas.Value, cambio.Nota);
                }
                else if (cambio.Horas is { } horas)
                {
                    if (!entrenador.Activo)
                        throw new ArgumentException($"{entrenador.NombreCompleto} está desactivado: no se le pueden cargar días nuevos.");
                    if (entrenador.ValorHora is null)
                        throw new ArgumentException($"Cargá el valor de la hora de {entrenador.NombreCompleto} antes de cargarle horas.");
                    // El día nace con el valor de la hora de hoy, y desde ahí no cambia aunque el de la ficha suba.
                    context.HorasDeEntrenador.Add(new HoraDeEntrenador(entrenador.Id, cambio.Fecha, horas, entrenador.ValorHora.Value, cambio.Nota, hoy));
                }
                // Borrar un día que no existe no es un error: el resultado pedido ya es el que hay.
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task<ResultadoDePrecarga> PrecargarAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken)
        {
            ReglasDeHoras.ValidarPeriodo(desde, hasta);

            var hoy = Hoy();
            var ultimo = hasta < hoy ? hasta : hoy;   // lo que todavía no pasó no se carga
            if (desde > ultimo) return new ResultadoDePrecarga(0, Array.Empty<string>());

            var activos = await context.Entrenadores.AsNoTracking().Where(e => e.Activo).OrderBy(e => e.NombreCompleto).ToListAsync(cancellationToken);
            var turnos = await TurnosAsync(cancellationToken);
            var cargados = (await context.HorasDeEntrenador.AsNoTracking()
                    .Where(h => h.Fecha >= desde && h.Fecha <= ultimo)
                    .Select(h => new { h.EntrenadorId, h.Fecha })
                    .ToListAsync(cancellationToken))
                .Select(h => (h.EntrenadorId, h.Fecha))
                .ToHashSet();

            var creados = 0;
            var sinValor = new List<string>();
            foreach (var entrenador in activos)
            {
                var faltaElValor = false;
                foreach (var fecha in Dias(desde, ultimo))
                {
                    var programadas = ReglasDeHoras.Programadas(turnos, entrenador.Id, fecha);
                    if (programadas == 0 || cargados.Contains((entrenador.Id, fecha))) continue;   // sin horas ese día, o ya cargado: no se pisa
                    if (entrenador.ValorHora is null) { faltaElValor = true; continue; }
                    context.HorasDeEntrenador.Add(new HoraDeEntrenador(entrenador.Id, fecha, programadas, entrenador.ValorHora.Value, null, hoy));
                    creados++;
                }
                if (faltaElValor) sinValor.Add(entrenador.NombreCompleto);
            }

            await context.SaveChangesAsync(cancellationToken);
            return new ResultadoDePrecarga(creados, sinValor);
        }

        public async Task<Presupuesto> PresupuestoAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken)
        {
            ReglasDeHoras.ValidarPeriodo(desde, hasta);

            var dias = await (
                from h in context.HorasDeEntrenador.AsNoTracking()
                where h.Fecha >= desde && h.Fecha <= hasta
                join e in context.Entrenadores.AsNoTracking() on h.EntrenadorId equals e.Id
                select new { h.EntrenadorId, e.NombreCompleto, h.Horas, h.ValorHora })
                .ToListAsync(cancellationToken);

            var filas = dias
                .GroupBy(d => (d.EntrenadorId, d.NombreCompleto))
                .OrderBy(g => g.Key.NombreCompleto, Alfabetico)
                .Select(g =>
                {
                    var conHoras = g.Where(d => d.Horas > 0).ToList();
                    // «El valor de la hora» de la fila es el de los días que se pagan; un día que faltó (0 horas) no lo vuelve «varios».
                    var valores = (conHoras.Count > 0 ? conHoras : g.ToList()).Select(d => d.ValorHora).Distinct().ToList();
                    return new FilaDePresupuesto(
                        g.Key.EntrenadorId, g.Key.NombreCompleto, conHoras.Count, g.Sum(d => d.Horas),
                        valores.Count == 1 ? valores[0] : null,
                        g.Sum(d => ReglasDeHoras.MontoDelDia(d.Horas, d.ValorHora)));   // cada día se redondea solo: el total es la suma exacta
                })
                .ToList();

            return new Presupuesto(desde, hasta, filas, filas.Sum(f => f.Horas), filas.Sum(f => f.Monto));
        }
    }
}
