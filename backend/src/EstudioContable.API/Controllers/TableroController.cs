using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.API.Auth;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.API.Controllers
{
    /// <summary>
    /// Tablero de Gestión: indicadores con meta y semáforo, análisis y apoyo a las decisiones. Son números del negocio:
    /// solo Administrador y Contador. Las metas las fija únicamente el Administrador.
    /// </summary>
    [ApiController]
    [Route("api/tablero")]
    [Authorize(Roles = Roles.Gestion)]
    public class TableroController : ControllerBase
    {
        private readonly IAppDbContext _context;
        private readonly IUsuarioActual _usuario;
        private readonly TimeProvider _reloj;

        public TableroController(IAppDbContext context, IUsuarioActual usuario, TimeProvider reloj)
        {
            _context = context;
            _usuario = usuario;
            _reloj = reloj;
        }

        public record GuardarMetaRequest(decimal? Valor);

        [HttpGet("metas")]
        public async Task<IActionResult> Metas(CancellationToken cancellationToken)
            => Ok(await _context.MetasDeIndicadores.AsNoTracking()
                .OrderBy(m => m.Clave)
                .Select(m => new { m.Clave, m.Valor, m.ActualizadoPor, m.ActualizadoEn })
                .ToListAsync(cancellationToken));

        /// <summary>Fija (o cambia) la meta de un indicador; con valor vacío la borra.</summary>
        [HttpPut("metas/{clave}")]
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> GuardarMeta(string clave, [FromBody] GuardarMetaRequest request, CancellationToken cancellationToken)
        {
            var ahora = _reloj.GetUtcNow().UtcDateTime;
            var usuario = _usuario.Email ?? "sistema";
            var meta = await _context.MetasDeIndicadores.FirstOrDefaultAsync(m => m.Clave == clave, cancellationToken);
            if (request.Valor is null)
            {
                if (meta is not null) _context.MetasDeIndicadores.Remove(meta);
            }
            else if (meta is null)
                _context.MetasDeIndicadores.Add(new MetaDeIndicador(clave, request.Valor.Value, usuario, ahora));
            else
                meta.Cambiar(request.Valor.Value, usuario, ahora);
            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
    }
}
