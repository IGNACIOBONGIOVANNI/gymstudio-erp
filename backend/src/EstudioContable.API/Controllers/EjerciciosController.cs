using System;
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
    /// <summary>Catálogo de ejercicios que se combinan en las rutinas.</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class EjerciciosController : ControllerBase
    {
        private readonly IAppDbContext _context;

        public EjerciciosController(IAppDbContext context) => _context = context;

        public record EjercicioRequest(string? Nombre, string? GrupoMuscular, bool? Activo);

        [HttpGet]
        public async Task<IActionResult> Listar([FromQuery] bool incluirInactivos, CancellationToken cancellationToken)
        {
            var lista = await _context.Ejercicios.AsNoTracking()
                .Where(e => incluirInactivos || e.Activo)
                .OrderBy(e => e.GrupoMuscular).ThenBy(e => e.Nombre)
                .Select(e => new { e.Id, e.Nombre, e.GrupoMuscular, e.Activo })
                .ToListAsync(cancellationToken);

            var usos = await _context.RutinaEjercicios.AsNoTracking()
                .GroupBy(r => r.EjercicioId)
                .Select(g => new { EjercicioId = g.Key, Rutinas = g.Select(x => x.RutinaId).Distinct().Count() })
                .ToDictionaryAsync(x => x.EjercicioId, x => x.Rutinas, cancellationToken);

            return Ok(lista.Select(e => new { e.Id, e.Nombre, e.GrupoMuscular, e.Activo, EnRutinas = usos.GetValueOrDefault(e.Id) }));
        }

        [HttpGet("grupos")]
        public IActionResult Grupos() => Ok(Ejercicio.GruposMusculares);

        [Authorize(Roles = Roles.Gestion)]
        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] EjercicioRequest request, CancellationToken cancellationToken)
        {
            var ejercicio = new Ejercicio(request.Nombre ?? string.Empty, request.GrupoMuscular ?? string.Empty);
            await ValidarNombreNoRepetidoAsync(ejercicio.Nombre, null, cancellationToken);

            _context.Ejercicios.Add(ejercicio);
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(new { ejercicio.Id, ejercicio.Nombre, ejercicio.GrupoMuscular, ejercicio.Activo });
        }

        [Authorize(Roles = Roles.Gestion)]
        [HttpPut("{id:long}")]
        public async Task<IActionResult> Actualizar(long id, [FromBody] EjercicioRequest request, CancellationToken cancellationToken)
        {
            var ejercicio = await _context.Ejercicios.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
                ?? throw new NotFoundException("Ejercicio", id);

            ejercicio.Actualizar(request.Nombre ?? string.Empty, request.GrupoMuscular ?? string.Empty);
            if (request.Activo == true) ejercicio.Activar();
            else if (request.Activo == false) ejercicio.Desactivar();

            await ValidarNombreNoRepetidoAsync(ejercicio.Nombre, id, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        /// <summary>Sin uso se elimina; si está en alguna rutina se desactiva para no romperla.</summary>
        [Authorize(Roles = Roles.Gestion)]
        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Eliminar(long id, CancellationToken cancellationToken)
        {
            var ejercicio = await _context.Ejercicios.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
                ?? throw new NotFoundException("Ejercicio", id);

            if (await _context.RutinaEjercicios.AnyAsync(r => r.EjercicioId == id, cancellationToken))
            {
                ejercicio.Desactivar();
                await _context.SaveChangesAsync(cancellationToken);
                return Ok(new { accion = "desactivado" });
            }

            _context.Ejercicios.Remove(ejercicio);
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(new { accion = "eliminado" });
        }

        private async Task ValidarNombreNoRepetidoAsync(string nombre, long? idExcluido, CancellationToken cancellationToken)
        {
            var minuscula = nombre.ToLower();
            var repetido = await _context.Ejercicios.AsNoTracking()
                .Where(e => e.Nombre.ToLower() == minuscula && e.Id != idExcluido)
                .Select(e => new { e.Activo })
                .FirstOrDefaultAsync(cancellationToken);
            if (repetido is not null)
                throw new InvalidOperationException(repetido.Activo
                    ? $"Ya existe el ejercicio \"{nombre}\"."
                    : $"Ya existe el ejercicio \"{nombre}\", pero está desactivado: editalo para volver a activarlo.");
        }
    }
}
