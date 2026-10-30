using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.API.Auth;
using EstudioContable.Application.Copias;
using EstudioContable.Application.Interfaces;
using EstudioContable.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EstudioContable.API.Controllers
{
    /// <summary>
    /// Copia de seguridad completa desde la propia pantalla: descargar (todas las tablas en un .zip), comparar con el sistema
    /// actual (solo lectura) y restaurar. Solo Administrador. Cada descarga y cada restauración quedan en la auditoría.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = Roles.Administrador)]
    public class CopiasController : ControllerBase
    {
        /// <summary>Lo que hay que escribir para confirmar que se reemplazan TODOS los datos.</summary>
        public const string Confirmacion = "RESTAURAR";

        // Un poco más que la copia: el resto del formulario también cuenta.
        private const long TamanoMaximoDelPedido = 55_000_000;

        private readonly ICopiasDeSeguridadService _copias;
        private readonly IRegistroDeEventos _eventos;
        private readonly IUsuarioActual _usuario;
        private readonly IControlIntegridadService _integridad;

        public CopiasController(ICopiasDeSeguridadService copias, IRegistroDeEventos eventos, IUsuarioActual usuario, IControlIntegridadService integridad)
        {
            _copias = copias;
            _eventos = eventos;
            _usuario = usuario;
            _integridad = integridad;
        }

        [HttpGet("descargar")]
        public async Task<IActionResult> Descargar(CancellationToken cancellationToken)
        {
            var zip = await _copias.GenerarAsync(cancellationToken);
            await _eventos.RegistrarAsync("copia", "DESCARGA", _usuario.Email ?? "sistema", cancellationToken);
            return File(zip, "application/zip", $"gymstudio_copia_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip");
        }

        [HttpPost("comparar")]
        [RequestSizeLimit(TamanoMaximoDelPedido)]
        [RequestFormLimits(MultipartBodyLengthLimit = TamanoMaximoDelPedido)]
        public async Task<ActionResult<ComparacionDeCopia>> Comparar(IFormFile file, CancellationToken cancellationToken)
        {
            ValidarArchivo(file);
            await using var contenido = file.OpenReadStream();
            return Ok(await _copias.CompararAsync(contenido, cancellationToken));
        }

        [HttpPost("restaurar")]
        [RequestSizeLimit(TamanoMaximoDelPedido)]
        [RequestFormLimits(MultipartBodyLengthLimit = TamanoMaximoDelPedido)]
        public async Task<IActionResult> Restaurar(IFormFile file, [FromForm] string? confirmacion, CancellationToken cancellationToken)
        {
            if (!string.Equals(confirmacion?.Trim(), Confirmacion, StringComparison.Ordinal))
                throw new ArgumentException($"Para restaurar tenés que escribir {Confirmacion} para confirmar: se reemplazan todos los datos del sistema.");
            ValidarArchivo(file);

            ResultadoDeRestauracion resultado;
            await using (var contenido = file.OpenReadStream())
                resultado = await _copias.RestaurarAsync(contenido, cancellationToken);

            await _eventos.RegistrarAsync("copia", "RESTAURADA", _usuario.Email ?? "sistema", cancellationToken);
            var integridad = await _integridad.EjecutarAsync(cancellationToken);
            return Ok(new { resultado.Tablas, resultado.Filas, Integridad = new { integridad.Total, integridad.Correctos } });
        }

        private static void ValidarArchivo(IFormFile? file)
        {
            if (file == null || file.Length == 0) throw new ArgumentException("No se proporcionó ninguna copia.");
            if (!string.Equals(Path.GetExtension(file.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("La copia tiene que ser un archivo .zip descargado desde el sistema.");
        }
    }
}
