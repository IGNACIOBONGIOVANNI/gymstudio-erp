using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.API.Auth;
using EstudioContable.Application.Common.Exceptions;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.API.Controllers
{
    /// <summary>Rutinas modelo: días de entrenamiento con sus ejercicios, series y repeticiones.</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class RutinasController : ControllerBase
    {
        private readonly IAppDbContext _context;

        public RutinasController(IAppDbContext context) => _context = context;

        public record RutinaEjercicioRequest(long EjercicioId, int Dia, int Series, string? Repeticiones, int DescansoSegundos, string? Indicaciones);
        public record RutinaRequest(string? Nombre, string? Objetivo, string? Nivel, bool? Activa, List<RutinaEjercicioRequest>? Ejercicios);

        [HttpGet]
        public async Task<IActionResult> Listar([FromQuery] bool incluirInactivas, CancellationToken cancellationToken)
        {
            var lista = await _context.Rutinas.AsNoTracking()
                .Where(r => incluirInactivas || r.Activa)
                .OrderBy(r => r.Nombre)
                .Select(r => new
                {
                    r.Id,
                    r.Nombre,
                    r.Objetivo,
                    r.Nivel,
                    r.Activa,
                    Ejercicios = r.Ejercicios.Count,
                    Dias = r.Ejercicios.Select(e => e.Dia).Distinct().Count(),
                    SociosActivos = _context.AsignacionesRutina.Count(a => a.RutinaId == r.Id && a.Activa)
                })
                .ToListAsync(cancellationToken);
            return Ok(lista);
        }

        [HttpGet("niveles")]
        public IActionResult Niveles() => Ok(Rutina.Niveles);

        [HttpGet("{id:long}")]
        public async Task<IActionResult> Detalle(long id, CancellationToken cancellationToken)
        {
            var rutina = await _context.Rutinas.AsNoTracking()
                .Include(r => r.Ejercicios).ThenInclude(e => e.Ejercicio)
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
                ?? throw new NotFoundException("Rutina", id);
            return Ok(RutinaDtos.Detalle(rutina));
        }

        [Authorize(Roles = Roles.Gestion)]
        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] RutinaRequest request, CancellationToken cancellationToken)
        {
            var lineas = await ConstruirLineasAsync(request, rutinaId: null, cancellationToken);
            var rutina = new Rutina(request.Nombre ?? string.Empty, request.Objetivo, request.Nivel ?? string.Empty, lineas);
            await ValidarNombreNoRepetidoAsync(rutina.Nombre, null, cancellationToken);

            _context.Rutinas.Add(rutina);
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(new { rutina.Id });
        }

        [Authorize(Roles = Roles.Gestion)]
        [HttpPut("{id:long}")]
        public async Task<IActionResult> Actualizar(long id, [FromBody] RutinaRequest request, CancellationToken cancellationToken)
        {
            var rutina = await _context.Rutinas.Include(r => r.Ejercicios).FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
                ?? throw new NotFoundException("Rutina", id);

            var lineas = await ConstruirLineasAsync(request, id, cancellationToken);
            rutina.Actualizar(request.Nombre ?? string.Empty, request.Objetivo, request.Nivel ?? string.Empty, lineas);
            if (request.Activa == true) rutina.Activar();
            else if (request.Activa == false) rutina.Desactivar();
            await ValidarNombreNoRepetidoAsync(rutina.Nombre, id, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        /// <summary>Sin socios en su historial se elimina; si alguna vez se asignó, se desactiva.</summary>
        [Authorize(Roles = Roles.Gestion)]
        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Eliminar(long id, CancellationToken cancellationToken)
        {
            var rutina = await _context.Rutinas.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
                ?? throw new NotFoundException("Rutina", id);

            if (await _context.AsignacionesRutina.AnyAsync(a => a.RutinaId == id, cancellationToken))
            {
                rutina.Desactivar();
                await _context.SaveChangesAsync(cancellationToken);
                return Ok(new { accion = "desactivada" });
            }

            _context.Rutinas.Remove(rutina);
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(new { accion = "eliminada" });
        }

        /// <summary>
        /// Arma los renglones y comprueba que cada ejercicio exista. Un ejercicio desactivado solo
        /// se admite si ya formaba parte de la rutina que se está editando.
        /// </summary>
        private async Task<List<RutinaEjercicio>> ConstruirLineasAsync(RutinaRequest request, long? rutinaId, CancellationToken cancellationToken)
        {
            var pedidos = request.Ejercicios ?? new List<RutinaEjercicioRequest>();
            if (pedidos.Any(p => p is null))
                throw new ArgumentException("Cada renglón de la rutina debe indicar su ejercicio, día, series y repeticiones.");
            if (pedidos.Count > Rutina.MaximoDeRenglones)
                throw new ArgumentException($"Una rutina no puede tener más de {Rutina.MaximoDeRenglones} ejercicios.");
            var ids = pedidos.Select(p => p.EjercicioId).Distinct().ToList();

            var ejercicios = await _context.Ejercicios.AsNoTracking()
                .Where(e => ids.Contains(e.Id))
                .Select(e => new { e.Id, e.Nombre, e.Activo })
                .ToDictionaryAsync(e => e.Id, cancellationToken);

            var yaEnLaRutina = rutinaId is long rid
                ? (await _context.RutinaEjercicios.AsNoTracking().Where(r => r.RutinaId == rid).Select(r => r.EjercicioId).ToListAsync(cancellationToken)).ToHashSet()
                : new HashSet<long>();

            foreach (var id in ids)
            {
                if (!ejercicios.TryGetValue(id, out var e))
                    throw new ArgumentException("Uno de los ejercicios elegidos no existe.");
                if (!e.Activo && !yaEnLaRutina.Contains(id))
                    throw new ArgumentException($"El ejercicio \"{e.Nombre}\" está desactivado.");
            }

            return pedidos
                .Select(p => new RutinaEjercicio(p.EjercicioId, p.Dia, p.Series, p.Repeticiones ?? string.Empty, p.DescansoSegundos, p.Indicaciones))
                .ToList();
        }

        private async Task ValidarNombreNoRepetidoAsync(string nombre, long? idExcluido, CancellationToken cancellationToken)
        {
            var minuscula = nombre.ToLower();
            if (await _context.Rutinas.AsNoTracking().AnyAsync(r => r.Nombre.ToLower() == minuscula && r.Id != idExcluido, cancellationToken))
                throw new InvalidOperationException($"Ya existe una rutina llamada \"{nombre}\".");
        }
    }

    /// <summary>Formato único con el que la API devuelve una rutina completa (pantalla y PDF).</summary>
    public static class RutinaDtos
    {
        public static object Detalle(Rutina r) => new
        {
            r.Id,
            r.Nombre,
            r.Objetivo,
            r.Nivel,
            r.Activa,
            Ejercicios = r.Ejercicios
                .OrderBy(e => e.Dia).ThenBy(e => e.Orden)
                .Select(e => new
                {
                    e.EjercicioId,
                    Ejercicio = e.Ejercicio?.Nombre,
                    GrupoMuscular = e.Ejercicio?.GrupoMuscular,
                    e.Dia,
                    e.Series,
                    e.Repeticiones,
                    e.DescansoSegundos,
                    e.Indicaciones
                })
        };
    }
}
