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
    /// <summary>
    /// Entrenadores del gimnasio: fichas que se ponen en el calendario semanal (no son usuarios del sistema). Las ven todos los
    /// usuarios; las cargan, editan, activan y desactivan el Administrador y el Contador. Un entrenador no se borra: se desactiva,
    /// y solo si no tiene horas en el calendario.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class EntrenadoresController : ControllerBase
    {
        private readonly IAppDbContext _context;

        public EntrenadoresController(IAppDbContext context) => _context = context;

        public record EntrenadorDto(long Id, string Nombre, string? Telefono, string? Especialidad, bool Activo, int HorasSemanales);

        public record GuardarEntrenadorRequest(string? Nombre, string? Telefono, string? Especialidad);

        public record CambiarEstadoRequest(bool Activo);

        [HttpGet]
        public async Task<IActionResult> Listar([FromQuery] bool soloActivos, CancellationToken cancellationToken)
        {
            var consulta = _context.Entrenadores.AsNoTracking();
            if (soloActivos) consulta = consulta.Where(e => e.Activo);
            var fichas = await consulta.OrderBy(e => e.NombreCompleto).ToListAsync(cancellationToken);

            var horas = await _context.TurnosEncargado.AsNoTracking()
                .GroupBy(t => t.EntrenadorId)
                .Select(g => new { EntrenadorId = g.Key, Horas = g.Count() })
                .ToDictionaryAsync(g => g.EntrenadorId, g => g.Horas, cancellationToken);

            return Ok(fichas.Select(e => new EntrenadorDto(
                e.Id, e.NombreCompleto, e.Telefono, e.Especialidad, e.Activo, horas.GetValueOrDefault(e.Id))));
        }

        [Authorize(Roles = Roles.Gestion)]
        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] GuardarEntrenadorRequest request, CancellationToken cancellationToken)
        {
            var entrenador = new Entrenador(request.Nombre, request.Telefono, request.Especialidad);
            await ValidarNombreLibreAsync(entrenador.NombreCompleto, idExcluido: null, cancellationToken);

            _context.Entrenadores.Add(entrenador);
            await _context.SaveChangesAsync(cancellationToken);
            return Created($"/api/entrenadores/{entrenador.Id}", new { id = entrenador.Id });
        }

        [Authorize(Roles = Roles.Gestion)]
        [HttpPut("{id:long}")]
        public async Task<IActionResult> Actualizar(long id, [FromBody] GuardarEntrenadorRequest request, CancellationToken cancellationToken)
        {
            var entrenador = await BuscarAsync(id, cancellationToken);
            entrenador.ActualizarDatos(request.Nombre, request.Telefono, request.Especialidad);
            await ValidarNombreLibreAsync(entrenador.NombreCompleto, id, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = Roles.Gestion)]
        [HttpPut("{id:long}/estado")]
        public async Task<IActionResult> CambiarEstado(long id, [FromBody] CambiarEstadoRequest request, CancellationToken cancellationToken)
        {
            var entrenador = await BuscarAsync(id, cancellationToken);
            if (request.Activo)
            {
                entrenador.Activar();
            }
            else
            {
                var horas = await _context.TurnosEncargado.CountAsync(t => t.EntrenadorId == id, cancellationToken);
                if (horas > 0)
                    throw new InvalidOperationException(
                        $"{entrenador.NombreCompleto} tiene {horas} {(horas == 1 ? "hora" : "horas")} en el calendario por semana. Liberalas antes de desactivarlo.");
                entrenador.Desactivar();
            }

            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        private async Task<Entrenador> BuscarAsync(long id, CancellationToken cancellationToken)
            => await _context.Entrenadores.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
               ?? throw new NotFoundException(nameof(Entrenador), id);

        /// <summary>
        /// El nombre no se repite aunque cambien las mayúsculas (el nombre ya viene sin espacios repetidos). Se compara acá, y no
        /// solo en la base, porque lower() de PostgreSQL depende de la configuración regional y no siempre pliega los acentos;
        /// el índice único ux_entrenadores_nombre queda de respaldo.
        /// </summary>
        private async Task ValidarNombreLibreAsync(string nombre, long? idExcluido, CancellationToken cancellationToken)
        {
            var existentes = await _context.Entrenadores.AsNoTracking()
                .Select(e => new { e.Id, e.NombreCompleto })
                .ToListAsync(cancellationToken);
            if (existentes.Any(e => e.Id != idExcluido && string.Equals(e.NombreCompleto, nombre, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Ya hay un entrenador con ese nombre.");
        }
    }
}
