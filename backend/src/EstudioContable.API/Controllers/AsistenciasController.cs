using System;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.API.Auth;
using EstudioContable.Application.Common.Exceptions;
using EstudioContable.Application.Interfaces;
using EstudioContable.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.API.Controllers
{
    /// <summary>
    /// Ingreso de socios al gimnasio. Cualquier usuario con sesión (también Recepción) registra ingresos y consulta
    /// los del día y los socios en riesgo de abandono; borrar un ingreso cargado por error es solo de gestión.
    /// </summary>
    [ApiController]
    [Route("api/asistencias")]
    public class AsistenciasController : ControllerBase
    {
        private readonly IAsistenciasService _asistencias;
        private readonly IAppDbContext _context;

        public AsistenciasController(IAsistenciasService asistencias, IAppDbContext context)
        {
            _asistencias = asistencias;
            _context = context;
        }

        public record IngresoRequest(string? Dni);

        private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3)); // Argentina

        /// <summary>Registra el ingreso del socio con ese DNI y devuelve cómo está su cuota y qué día de su rutina le toca.</summary>
        [HttpPost("ingreso")]
        public async Task<IActionResult> Ingreso([FromBody] IngresoRequest r, CancellationToken cancellationToken)
            => Ok(await _asistencias.RegistrarIngresoAsync(r?.Dni, Hoy, cancellationToken));

        [HttpGet("hoy")]
        public async Task<IActionResult> IngresosDeHoy(CancellationToken cancellationToken)
            => Ok(await _asistencias.IngresosDelDiaAsync(Hoy, cancellationToken));

        /// <param name="dias">Días sin venir a partir de los cuales se lo considera en riesgo (por defecto 14).</param>
        [HttpGet("en-riesgo")]
        public async Task<IActionResult> EnRiesgo([FromQuery] int? dias, CancellationToken cancellationToken)
            => Ok(await _asistencias.SociosEnRiesgoAsync(Hoy, dias ?? EstudioContable.Domain.ReglasDeAsistencia.DiasSinVenirPorDefecto, cancellationToken));

        [Authorize(Roles = Roles.Gestion)]
        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Eliminar(long id, CancellationToken cancellationToken)
        {
            var asistencia = await _context.AsistenciasSocio.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
                ?? throw new NotFoundException("Ingreso", id);
            _context.AsistenciasSocio.Remove(asistencia);
            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
    }
}
