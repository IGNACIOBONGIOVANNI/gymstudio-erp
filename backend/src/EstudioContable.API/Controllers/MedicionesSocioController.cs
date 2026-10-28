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
    /// <summary>
    /// Seguimiento de peso y medidas de cada socio. Cualquier usuario con sesión (también Recepción)
    /// registra y corrige mediciones; borrarlas es solo de gestión.
    /// </summary>
    [ApiController]
    public class MedicionesSocioController : ControllerBase
    {
        private readonly IAppDbContext _context;

        public MedicionesSocioController(IAppDbContext context) => _context = context;

        public record MedicionRequest(DateOnly? Fecha, decimal PesoKg, decimal? GrasaCorporalPct, decimal? CinturaCm,
            decimal? CaderaCm, decimal? PechoCm, decimal? BrazoCm, decimal? MusloCm, string? Notas);

        private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3)); // Argentina

        private static object Dto(MedicionSocio m) => new
        {
            m.Id, m.Fecha, m.PesoKg, m.GrasaCorporalPct, m.CinturaCm, m.CaderaCm, m.PechoCm, m.BrazoCm, m.MusloCm, m.Notas
        };

        [HttpGet("api/socios/{socioId:long}/mediciones")]
        public async Task<IActionResult> Listar(long socioId, CancellationToken cancellationToken)
        {
            if (!await _context.Socios.AnyAsync(s => s.Id == socioId, cancellationToken))
                throw new NotFoundException("Socio", socioId);

            var lista = await _context.MedicionesSocio.AsNoTracking()
                .Where(m => m.SocioId == socioId)
                .OrderBy(m => m.Fecha)
                .ToListAsync(cancellationToken);
            return Ok(lista.Select(Dto));
        }

        [HttpPost("api/socios/{socioId:long}/mediciones")]
        public async Task<IActionResult> Crear(long socioId, [FromBody] MedicionRequest r, CancellationToken cancellationToken)
        {
            var socio = await _context.Socios.AsNoTracking().Where(s => s.Id == socioId)
                .Select(s => new { s.Nombre, s.Apellido, s.Activo }).FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Socio", socioId);
            if (!socio.Activo)
                throw new InvalidOperationException($"{socio.Apellido} {socio.Nombre} está dado de baja.");

            var fecha = r.Fecha ?? throw new ArgumentException("Indicá la fecha de la medición.");
            var medicion = new MedicionSocio(socioId, fecha, r.PesoKg, r.GrasaCorporalPct, r.CinturaCm, r.CaderaCm, r.PechoCm, r.BrazoCm, r.MusloCm, r.Notas, Hoy);
            await ValidarUnaPorDiaAsync(socioId, fecha, null, cancellationToken);

            _context.MedicionesSocio.Add(medicion);
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(Dto(medicion));
        }

        [HttpPut("api/mediciones/{id:long}")]
        public async Task<IActionResult> Actualizar(long id, [FromBody] MedicionRequest r, CancellationToken cancellationToken)
        {
            var medicion = await _context.MedicionesSocio.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
                ?? throw new NotFoundException("Medición", id);

            var fecha = r.Fecha ?? throw new ArgumentException("Indicá la fecha de la medición.");
            medicion.Actualizar(fecha, r.PesoKg, r.GrasaCorporalPct, r.CinturaCm, r.CaderaCm, r.PechoCm, r.BrazoCm, r.MusloCm, r.Notas, Hoy);
            await ValidarUnaPorDiaAsync(medicion.SocioId, fecha, id, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = Roles.Gestion)]
        [HttpDelete("api/mediciones/{id:long}")]
        public async Task<IActionResult> Eliminar(long id, CancellationToken cancellationToken)
        {
            var medicion = await _context.MedicionesSocio.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
                ?? throw new NotFoundException("Medición", id);
            _context.MedicionesSocio.Remove(medicion);
            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        private async Task ValidarUnaPorDiaAsync(long socioId, DateOnly fecha, long? idExcluido, CancellationToken cancellationToken)
        {
            if (await _context.MedicionesSocio.AsNoTracking().AnyAsync(m => m.SocioId == socioId && m.Fecha == fecha && m.Id != idExcluido, cancellationToken))
                throw new InvalidOperationException($"Ya hay una medición del {fecha:dd/MM/yyyy}: editala en lugar de cargar otra.");
        }
    }
}
