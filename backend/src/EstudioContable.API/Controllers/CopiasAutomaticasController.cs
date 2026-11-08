using System.Threading;
using System.Threading.Tasks;
using EstudioContable.API.Auth;
using EstudioContable.Application.Copias;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EstudioContable.API.Controllers
{
    /// <summary>
    /// Copias de seguridad automáticas: programarlas (cada 6 h, cada 12 h, diaria, semanal o mensual), hacer una en el momento, ver las
    /// guardadas y descargarlas. Solo el Administrador, igual que el resto de las copias. Cada cambio y cada copia quedan en la auditoría.
    /// </summary>
    [ApiController]
    [Route("api/copias")]
    [Authorize(Roles = Roles.Administrador)]
    public class CopiasAutomaticasController : ControllerBase
    {
        private readonly ICopiasAutomaticasService _copias;

        public CopiasAutomaticasController(ICopiasAutomaticasService copias) => _copias = copias;

        /// <summary>La programación vigente, cuándo es la próxima copia y cómo le fue a la última.</summary>
        [HttpGet("programacion")]
        public async Task<ActionResult<EstadoDeCopiasAutomaticas>> Obtener(CancellationToken cancellationToken)
            => Ok(await _copias.ObtenerAsync(cancellationToken));

        /// <summary>Cambia la programación. Si algo no sirve responde 400 y no cambia nada.</summary>
        [HttpPut("programacion")]
        public async Task<ActionResult<EstadoDeCopiasAutomaticas>> Programar([FromBody] ProgramacionDeCopias pedido, CancellationToken cancellationToken)
            => Ok(await _copias.ProgramarAsync(pedido, cancellationToken));

        /// <summary>Hace una copia en la carpeta en este momento, sin esperar a la programada.</summary>
        [HttpPost("programacion/ahora")]
        public async Task<ActionResult<CopiaGuardada>> HacerAhora(CancellationToken cancellationToken)
            => Ok(await _copias.GuardarAhoraAsync(cancellationToken));

        /// <summary>Las copias guardadas en la carpeta, de la más nueva a la más vieja.</summary>
        [HttpGet("automaticas")]
        public ActionResult<System.Collections.Generic.IReadOnlyList<CopiaGuardada>> Listar() => Ok(_copias.Listar());

        /// <summary>Descarga una copia guardada. El nombre se valida contra el formato exacto: nunca sale nada de la carpeta.</summary>
        [HttpGet("automaticas/{nombre}")]
        public IActionResult Descargar(string nombre)
        {
            var flujo = _copias.AbrirParaDescargar(nombre);
            return flujo == null ? NotFound() : File(flujo, "application/zip", nombre);
        }
    }
}
