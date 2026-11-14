using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.API.Auth;
using EstudioContable.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.API.Controllers
{
    /// <summary>
    /// Consulta del registro de auditoría que completa el trigger contabilidad.fn_auditar().
    /// Es de sólo lectura: la API no tiene forma de modificar la auditoría.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = Roles.Administrador)]
    public class AuditoriaController : ControllerBase
    {
        private const int MaximoRegistros = 500;
        private const int MaximoPorPagina = 100;
        private readonly IAppDbContext _context;

        public AuditoriaController(IAppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Listar(
            [FromQuery] string? tabla,
            [FromQuery] string? usuario,
            [FromQuery] int cantidad = 200,
            [FromQuery] int? pagina = null,
            [FromQuery] int tamano = 25,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Auditoria.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(tabla))
                query = query.Where(a => a.Tabla == tabla);
            if (!string.IsNullOrWhiteSpace(usuario))
                query = query.Where(a => a.Usuario.Contains(usuario));

            var ordenados = query.OrderByDescending(a => a.Id);
            List<EstudioContable.Domain.Entities.RegistroAuditoria> registros;
            if (pagina.HasValue)
            {
                // De a una página: la pantalla muestra «x a y de total» con el encabezado X-Total-Count.
                var porPagina = Math.Clamp(tamano, 1, MaximoPorPagina);
                var numero = Math.Max(pagina.Value, 1);
                Response.Headers["X-Total-Count"] = (await query.CountAsync(cancellationToken)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                registros = await ordenados.Skip((numero - 1) * porPagina).Take(porPagina).ToListAsync(cancellationToken);
            }
            else
            {
                registros = await ordenados.Take(Math.Clamp(cantidad, 1, MaximoRegistros)).ToListAsync(cancellationToken);
            }

            return Ok(registros.Select(a => new
            {
                a.Id,
                a.Fecha,
                a.Usuario,
                a.Tabla,
                a.Operacion,
                a.RegistroId,
                a.CamposModificados,
                a.Ip,
                a.SistemaOperativo,
                a.Navegador,
                datosAnteriores = Parsear(a.DatosAnteriores),
                datosNuevos = Parsear(a.DatosNuevos)
            }));
        }

        /// <summary>Tablas que tienen registros de auditoría (para el filtro de la pantalla).</summary>
        [HttpGet("tablas")]
        public async Task<IActionResult> Tablas(CancellationToken cancellationToken)
        {
            var tablas = await _context.Auditoria.AsNoTracking()
                .Select(a => a.Tabla).Distinct().OrderBy(t => t)
                .ToListAsync(cancellationToken);
            return Ok(tablas);
        }

        private static JsonElement? Parsear(string? json)
            => json == null ? null : JsonDocument.Parse(json).RootElement.Clone();
    }
}
