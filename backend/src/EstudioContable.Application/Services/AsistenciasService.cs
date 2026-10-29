using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain;
using EstudioContable.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Services
{
    public record SocioDelIngreso(long Id, string Nombre, string Apellido, string Dni);

    /// <summary>Estado de la cuota ("SinPlan", "AlDia", "PorVencer" o "Vencida") y, si tiene plan, cuándo vence.</summary>
    public record CuotaDelSocio(string Estado, DateOnly? Vence, int? DiasParaVencer);

    public record EjercicioDelDia(string Ejercicio, string GrupoMuscular, int Series, string Repeticiones, int DescansoSegundos, string Indicaciones);

    public record RutinaDelDia(long AsignacionId, string Nombre, string Objetivo, string Nivel, bool Vencida, DateOnly? Hasta,
        int? Dia, int DiasTotales, IReadOnlyList<EjercicioDelDia> Ejercicios);

    public record ResultadoIngreso(long Id, bool YaRegistrado, DateTime RegistradaEn, SocioDelIngreso Socio, CuotaDelSocio Cuota,
        int VisitasDelMes, DateOnly? UltimaVisitaAnterior, RutinaDelDia? Rutina);

    public record IngresoDeHoy(long Id, DateTime RegistradaEn, string RegistradaPor, SocioDelIngreso Socio, string Estado);

    public record SocioEnRiesgo(long Id, string Nombre, string Apellido, string Dni, string Telefono, DateOnly? UltimaVisita,
        int DiasSinVenir, DateOnly VenceCuota);

    public interface IAsistenciasService
    {
        Task<ResultadoIngreso> RegistrarIngresoAsync(string? dni, DateOnly hoy, CancellationToken cancellationToken);
        Task<IReadOnlyList<IngresoDeHoy>> IngresosDelDiaAsync(DateOnly hoy, CancellationToken cancellationToken);
        Task<IReadOnlyList<SocioEnRiesgo>> SociosEnRiesgoAsync(DateOnly hoy, int diasSinVenir, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Ingreso de socios al gimnasio: se identifican con el DNI, el sistema muestra cómo está su cuota y qué día de la
    /// rutina le toca, y deja registrada la visita. De las visitas sale el aviso de socios en riesgo de abandonar.
    /// </summary>
    public class AsistenciasService : IAsistenciasService
    {
        private readonly IAppDbContext _context;
        private readonly IUsuarioActual _usuario;

        public AsistenciasService(IAppDbContext context, IUsuarioActual usuario)
        {
            _context = context;
            _usuario = usuario;
        }

        private static DateTime InicioDelDia(DateOnly d) => new(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc);

        public async Task<ResultadoIngreso> RegistrarIngresoAsync(string? dni, DateOnly hoy, CancellationToken cancellationToken)
        {
            var numero = Socio.NormalizarDni(dni);
            if (numero.Length is < 7 or > 8 || !numero.All(char.IsDigit))
                throw new ArgumentException("Escribí el DNI del socio: 7 u 8 números.");

            var socio = await _context.Socios.AsNoTracking().FirstOrDefaultAsync(s => s.Dni == numero, cancellationToken)
                ?? throw new InvalidOperationException($"No hay ningún socio con el DNI {numero}.");
            if (!socio.Activo)
                throw new InvalidOperationException($"{socio.Apellido} {socio.Nombre} está dado de baja.");

            // Un ingreso por socio y día: si ya pasó, se muestra lo mismo sin duplicar la visita.
            var asistencia = await _context.AsistenciasSocio.AsNoTracking().FirstOrDefaultAsync(a => a.SocioId == socio.Id && a.Fecha == hoy, cancellationToken);
            var yaRegistrado = asistencia is not null;
            if (asistencia is null)
            {
                _context.AsistenciasSocio.Add(new AsistenciaSocio(socio.Id, hoy, _usuario.Email));
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    // Otra recepción lo registró justo en este momento (índice único de socio y día): vale ese.
                    asistencia = await _context.AsistenciasSocio.AsNoTracking().FirstOrDefaultAsync(a => a.SocioId == socio.Id && a.Fecha == hoy, cancellationToken);
                    if (asistencia is null) throw;
                    yaRegistrado = true;
                }
            }
            asistencia ??= await _context.AsistenciasSocio.AsNoTracking().FirstAsync(a => a.SocioId == socio.Id && a.Fecha == hoy, cancellationToken);

            var estado = ReglasDeAsistencia.CalcularEstado(socio.FechaVencimiento, hoy);
            DateOnly? vence = socio.FechaVencimiento is DateTime v ? DateOnly.FromDateTime(v) : null;
            var cuota = new CuotaDelSocio(estado.ToString(), vence, vence is DateOnly dv ? dv.DayNumber - hoy.DayNumber : null);

            var primeroDelMes = new DateOnly(hoy.Year, hoy.Month, 1);
            var visitasDelMes = await _context.AsistenciasSocio.CountAsync(a => a.SocioId == socio.Id && a.Fecha >= primeroDelMes && a.Fecha <= hoy, cancellationToken);
            var anterior = await _context.AsistenciasSocio.Where(a => a.SocioId == socio.Id && a.Fecha < hoy).MaxAsync(a => (DateOnly?)a.Fecha, cancellationToken);

            return new ResultadoIngreso(asistencia.Id, yaRegistrado, asistencia.RegistradaEn,
                new SocioDelIngreso(socio.Id, socio.Nombre, socio.Apellido, socio.Dni), cuota, visitasDelMes, anterior,
                await RutinaDeHoyAsync(socio.Id, hoy, cancellationToken));
        }

        private async Task<RutinaDelDia?> RutinaDeHoyAsync(long socioId, DateOnly hoy, CancellationToken cancellationToken)
        {
            var asignacion = await _context.AsignacionesRutina.AsNoTracking()
                .Where(a => a.SocioId == socioId && a.Activa)
                .Select(a => new { a.Id, a.RutinaId, a.FechaInicio, a.FechaFin })
                .FirstOrDefaultAsync(cancellationToken);
            if (asignacion is null) return null;

            var rutina = await _context.Rutinas.AsNoTracking().Where(r => r.Id == asignacion.RutinaId)
                .Select(r => new { r.Nombre, r.Objetivo, r.Nivel }).FirstOrDefaultAsync(cancellationToken);
            if (rutina is null) return null;

            var renglones = await _context.RutinaEjercicios.AsNoTracking().Include(e => e.Ejercicio)
                .Where(e => e.RutinaId == asignacion.RutinaId).OrderBy(e => e.Dia).ThenBy(e => e.Orden)
                .ToListAsync(cancellationToken);

            // La rotación cuenta las visitas anteriores a hoy desde que empezó esta rutina.
            var visitasPrevias = await _context.AsistenciasSocio.CountAsync(a => a.SocioId == socioId && a.Fecha >= asignacion.FechaInicio && a.Fecha < hoy, cancellationToken);
            var diasDeLaRutina = renglones.Select(e => e.Dia).Distinct().ToList();
            var dia = ReglasDeAsistencia.DiaDeRutina(diasDeLaRutina, visitasPrevias);

            var ejercicios = renglones.Where(e => e.Dia == dia)
                .Select(e => new EjercicioDelDia(e.Ejercicio?.Nombre ?? "Ejercicio", e.Ejercicio?.GrupoMuscular ?? string.Empty,
                    e.Series, e.Repeticiones, e.DescansoSegundos, e.Indicaciones))
                .ToList();

            return new RutinaDelDia(asignacion.Id, rutina.Nombre, rutina.Objetivo, rutina.Nivel,
                asignacion.FechaFin is DateOnly fin && fin < hoy, asignacion.FechaFin, dia, diasDeLaRutina.Count, ejercicios);
        }

        public async Task<IReadOnlyList<IngresoDeHoy>> IngresosDelDiaAsync(DateOnly hoy, CancellationToken cancellationToken)
        {
            var filas = await (from a in _context.AsistenciasSocio.AsNoTracking()
                               join s in _context.Socios.AsNoTracking() on a.SocioId equals s.Id
                               where a.Fecha == hoy
                               orderby a.RegistradaEn descending
                               select new { a.Id, a.RegistradaEn, a.RegistradaPor, SocioId = s.Id, s.Nombre, s.Apellido, s.Dni, s.FechaVencimiento })
                .ToListAsync(cancellationToken);

            return filas.Select(f => new IngresoDeHoy(f.Id, f.RegistradaEn, f.RegistradaPor,
                new SocioDelIngreso(f.SocioId, f.Nombre, f.Apellido, f.Dni),
                ReglasDeAsistencia.CalcularEstado(f.FechaVencimiento, hoy).ToString())).ToList();
        }

        public async Task<IReadOnlyList<SocioEnRiesgo>> SociosEnRiesgoAsync(DateOnly hoy, int diasSinVenir, CancellationToken cancellationToken)
        {
            var umbral = Math.Clamp(diasSinVenir, 1, 365);
            var inicioHoy = InicioDelDia(hoy);

            var vigentes = await _context.Socios.AsNoTracking()
                .Where(s => s.Activo && s.FechaVencimiento != null && s.FechaVencimiento >= inicioHoy)
                .Select(s => new
                {
                    s.Id, s.Nombre, s.Apellido, s.Dni, s.Telefono, s.FechaVencimiento, s.FechaAlta,
                    Ultima = _context.AsistenciasSocio.Where(a => a.SocioId == s.Id).Max(a => (DateOnly?)a.Fecha)
                })
                .ToListAsync(cancellationToken);

            return vigentes
                .Select(s => new
                {
                    Socio = s,
                    Dias = ReglasDeAsistencia.DiasSinVenir(s.Ultima, DateOnly.FromDateTime(s.FechaAlta), hoy),
                    Estado = ReglasDeAsistencia.CalcularEstado(s.FechaVencimiento, hoy)
                })
                .Where(x => ReglasDeAsistencia.EstaEnRiesgo(x.Estado, x.Dias, umbral))
                .OrderByDescending(x => x.Dias).ThenBy(x => x.Socio.Apellido).ThenBy(x => x.Socio.Nombre)
                .Select(x => new SocioEnRiesgo(x.Socio.Id, x.Socio.Nombre, x.Socio.Apellido, x.Socio.Dni, x.Socio.Telefono,
                    x.Socio.Ultima, x.Dias, DateOnly.FromDateTime(x.Socio.FechaVencimiento!.Value)))
                .ToList();
        }
    }
}
