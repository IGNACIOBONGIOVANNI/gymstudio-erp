using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Common.Exceptions;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.API.Controllers
{
    /// <summary>
    /// Rutinas de cada socio. Asignar y cerrar una rutina lo puede hacer cualquier usuario con sesión
    /// (también Recepción); armar el catálogo de rutinas y ejercicios es solo de gestión.
    /// </summary>
    [ApiController]
    public class AsignacionesRutinaController : ControllerBase
    {
        private readonly IAppDbContext _context;

        public AsignacionesRutinaController(IAppDbContext context) => _context = context;

        public record AsignarRutinaRequest(long RutinaId, DateOnly? FechaInicio, DateOnly? FechaFin, string? Observaciones);
        public record FinalizarRutinaRequest(DateOnly? Fecha);

        [HttpGet("api/socios/{socioId:long}/rutinas")]
        public async Task<IActionResult> Historial(long socioId, CancellationToken cancellationToken)
        {
            if (!await _context.Socios.AnyAsync(s => s.Id == socioId, cancellationToken))
                throw new NotFoundException("Socio", socioId);

            var historial = await _context.AsignacionesRutina.AsNoTracking()
                .Where(a => a.SocioId == socioId)
                .OrderByDescending(a => a.FechaInicio).ThenByDescending(a => a.Id)
                .Select(a => new
                {
                    a.Id,
                    a.RutinaId,
                    RutinaNombre = a.Rutina!.Nombre,
                    a.Rutina.Nivel,
                    a.FechaInicio,
                    a.FechaFin,
                    a.Observaciones,
                    a.Activa
                })
                .ToListAsync(cancellationToken);
            return Ok(historial);
        }

        [HttpPost("api/socios/{socioId:long}/rutinas")]
        public async Task<IActionResult> Asignar(long socioId, [FromBody] AsignarRutinaRequest request, CancellationToken cancellationToken)
        {
            var socio = await _context.Socios.AsNoTracking().Where(s => s.Id == socioId)
                .Select(s => new { s.Nombre, s.Apellido, s.Activo }).FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Socio", socioId);
            if (!socio.Activo)
                throw new InvalidOperationException($"{socio.Apellido} {socio.Nombre} está dado de baja: no se le puede asignar una rutina.");

            var rutina = await _context.Rutinas.AsNoTracking().Where(r => r.Id == request.RutinaId)
                .Select(r => new { r.Nombre, r.Activa }).FirstOrDefaultAsync(cancellationToken)
                ?? throw new ArgumentException("La rutina elegida no existe.");
            if (!rutina.Activa)
                throw new InvalidOperationException($"La rutina \"{rutina.Nombre}\" está desactivada.");

            var inicio = request.FechaInicio ?? throw new ArgumentException("Indicá la fecha de inicio de la rutina.");
            var nueva = new AsignacionRutina(socioId, request.RutinaId, inicio, request.FechaFin, request.Observaciones);

            // Una sola rutina activa por socio: la anterior se cierra el día que empieza la nueva
            // (o antes, si ya tenía un vencimiento anterior).
            await using var transaccion = await _context.Database.BeginTransactionAsync(cancellationToken);
            var anterior = await _context.AsignacionesRutina.FirstOrDefaultAsync(a => a.SocioId == socioId && a.Activa, cancellationToken);
            if (anterior is not null)
            {
                var cierre = anterior.FechaFin is DateOnly f && f < inicio ? f : inicio;
                anterior.Finalizar(cierre);
                await _context.SaveChangesAsync(cancellationToken);
            }

            _context.AsignacionesRutina.Add(nueva);
            await _context.SaveChangesAsync(cancellationToken);
            await transaccion.CommitAsync(cancellationToken);
            return Ok(new { nueva.Id });
        }

        /// <summary>La asignación completa con socio y ejercicios: es lo que se imprime.</summary>
        [HttpGet("api/asignaciones-rutina/{id:long}")]
        public async Task<IActionResult> Detalle(long id, CancellationToken cancellationToken)
        {
            var a = await _context.AsignacionesRutina.AsNoTracking()
                .Include(x => x.Socio)
                .Include(x => x.Rutina).ThenInclude(r => r!.Ejercicios).ThenInclude(e => e.Ejercicio)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new NotFoundException("Rutina del socio", id);

            return Ok(new
            {
                a.Id,
                a.FechaInicio,
                a.FechaFin,
                a.Observaciones,
                a.Activa,
                Socio = new { a.Socio!.Id, a.Socio.Nombre, a.Socio.Apellido, a.Socio.Dni },
                Rutina = RutinaDtos.Detalle(a.Rutina!)
            });
        }

        [HttpPost("api/asignaciones-rutina/{id:long}/finalizar")]
        public async Task<IActionResult> Finalizar(long id, [FromBody] FinalizarRutinaRequest? request, CancellationToken cancellationToken)
        {
            var asignacion = await _context.AsignacionesRutina.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
                ?? throw new NotFoundException("Rutina del socio", id);

            asignacion.Finalizar(request?.Fecha ?? DateOnly.FromDateTime(DateTime.Now));
            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
    }
}
