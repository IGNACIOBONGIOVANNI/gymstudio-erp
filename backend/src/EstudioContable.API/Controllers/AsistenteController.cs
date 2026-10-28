using System;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.API.Auth;
using EstudioContable.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace EstudioContable.API.Controllers
{
    /// <summary>
    /// Asistente por reglas: contesta preguntas frecuentes con los datos del sistema, sin internet ni IA externa.
    /// Cualquier usuario con sesión puede usarlo, pero lo financiero solo se le contesta a Administrador y Contador:
    /// el permiso se decide acá, en el servidor, con el rol del token.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AsistenteController : ControllerBase
    {
        public const int LargoMaximo = 300;

        private readonly IAsistenteService _asistente;

        public AsistenteController(IAsistenteService asistente) => _asistente = asistente;

        public record PreguntaRequest(string? Texto, DateOnly? Hoy);

        [HttpPost("preguntar")]
        public async Task<IActionResult> Preguntar([FromBody] PreguntaRequest request, CancellationToken cancellationToken)
        {
            var texto = request.Texto?.Trim();
            if (string.IsNullOrEmpty(texto))
                throw new ArgumentException("Escribí tu pregunta.");
            if (texto.Length > LargoMaximo)
                throw new ArgumentException($"La pregunta es demasiado larga (máximo {LargoMaximo} caracteres).");

            var hoy = request.Hoy ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3)); // Argentina
            if (hoy.Year < 2000 || hoy.Year > 2100)
                throw new ArgumentException("La fecha de hoy debe estar entre los años 2000 y 2100.");

            var veFinanzas = User.IsInRole(Roles.Administrador) || User.IsInRole(Roles.Contador);
            return Ok(await _asistente.PreguntarAsync(texto, veFinanzas, hoy, cancellationToken));
        }
    }
}
