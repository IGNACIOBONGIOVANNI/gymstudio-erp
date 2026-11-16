using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.API.Auth;
using EstudioContable.Application.Interfaces;
using EstudioContable.Application.Services;
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
        private readonly ITableroDeGestionService _tablero;

        public TableroController(IAppDbContext context, IUsuarioActual usuario, TimeProvider reloj, ITableroDeGestionService tablero)
        {
            _context = context;
            _usuario = usuario;
            _reloj = reloj;
            _tablero = tablero;
        }

        /// <summary>Indicadores del tablero de comando para un mes, comparados con el anterior y con su meta.</summary>
        /// <param name="mes">«aaaa-mm»; por defecto el mes de hoy.</param>
        /// <param name="hoy">Día de hoy del navegador; por defecto el de Argentina.</param>
        [HttpGet("indicadores")]
        public async Task<IActionResult> Indicadores([FromQuery] string? mes, [FromQuery] DateOnly? hoy, CancellationToken cancellationToken)
        {
            var dia = Hoy(hoy);
            var primerDia = Mes(mes, "mes") ?? new DateOnly(dia.Year, dia.Month, 1);
            return Ok(await _tablero.IndicadoresAsync(primerDia, dia, cancellationToken));
        }

        /// <summary>Análisis de un período (meses «aaaa-mm», hasta 24); con un solo mes, la evolución va por día.</summary>
        [HttpGet("analisis")]
        public async Task<IActionResult> Analisis([FromQuery] string? desde, [FromQuery] string? hasta, [FromQuery] long? planId,
            CancellationToken cancellationToken)
        {
            var dia = Hoy(null);
            var mesActual = new DateOnly(dia.Year, dia.Month, 1);
            var hastaMes = Mes(hasta, "mes «hasta»") ?? mesActual;
            var desdeMes = Mes(desde, "mes «desde»") ?? hastaMes.AddMonths(-5);
            return Ok(await _tablero.AnalisisAsync(desdeMes, hastaMes, planId, cancellationToken));
        }

        private DateOnly Hoy(DateOnly? hoy)
        {
            var dia = hoy ?? DateOnly.FromDateTime(_reloj.GetUtcNow().UtcDateTime.AddHours(-3));
            if (dia.Year < 2000 || dia.Year > 2100) throw new ArgumentException("La fecha de hoy debe estar entre los años 2000 y 2100.");
            return dia;
        }

        /// <summary>Lee un mes «aaaa-mm» y devuelve su primer día; vacío = null.</summary>
        internal static DateOnly? Mes(string? texto, string campo)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            if (!DateOnly.TryParseExact(texto.Trim() + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
                throw new ArgumentException($"El {campo} debe tener el formato aaaa-mm (por ejemplo 2026-10).");
            if (fecha.Year < 2000 || fecha.Year > 2100) throw new ArgumentException($"El {campo} debe estar entre los años 2000 y 2100.");
            return fecha;
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
