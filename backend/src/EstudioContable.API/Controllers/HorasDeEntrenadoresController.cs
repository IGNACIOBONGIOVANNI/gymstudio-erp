using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.API.Auth;
using EstudioContable.Application.Entrenadores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EstudioContable.API.Controllers
{
    /// <summary>
    /// Horas cumplidas por los entrenadores y presupuesto de pago por período. Es dinero: <b>todo</b> es solo del Administrador y el Contador
    /// (401 sin sesión, 403 a Recepción). Comparte la ruta <c>api/entrenadores</c> con las fichas; <c>horas</c> y <c>presupuesto</c> no chocan
    /// con <c>{id:long}</c>.
    /// </summary>
    [ApiController]
    [Route("api/entrenadores")]
    [Authorize(Roles = Roles.Gestion)]
    public class HorasDeEntrenadoresController : ControllerBase
    {
        private readonly IHorasDeEntrenadoresService _horas;

        public HorasDeEntrenadoresController(IHorasDeEntrenadoresService horas) => _horas = horas;

        public record RenglonDeHorasDto(long EntrenadorId, DateOnly Fecha, decimal? Horas, string? Nota);

        /// <summary>Un renglón nulo o la lista nula se rechazan con un 400 claro (el serializador los deja pasar).</summary>
        public record GuardarHorasRequest(List<RenglonDeHorasDto?>? Items);

        public record PrecargarHorasRequest(DateOnly Desde, DateOnly Hasta);

        [HttpGet("horas")]
        public async Task<IActionResult> Obtener([FromQuery] DateOnly desde, [FromQuery] DateOnly hasta, CancellationToken cancellationToken)
            => Ok(await _horas.ObtenerAsync(desde, hasta, cancellationToken));

        [HttpPut("horas")]
        public async Task<IActionResult> Guardar([FromBody] GuardarHorasRequest request, CancellationToken cancellationToken)
        {
            if (request.Items is null) throw new ArgumentException("Faltan los días a guardar.");
            if (request.Items.Any(i => i is null)) throw new ArgumentException("Hay un renglón vacío en el pedido.");

            await _horas.GuardarAsync(request.Items.Select(i => new CambioDeHoras(i!.EntrenadorId, i.Fecha, i.Horas, i.Nota)).ToList(), cancellationToken);
            return NoContent();
        }

        [HttpPost("horas/precargar")]
        public async Task<IActionResult> Precargar([FromBody] PrecargarHorasRequest request, CancellationToken cancellationToken)
            => Ok(await _horas.PrecargarAsync(request.Desde, request.Hasta, cancellationToken));

        [HttpGet("presupuesto")]
        public async Task<IActionResult> PresupuestoDelPeriodo([FromQuery] DateOnly desde, [FromQuery] DateOnly hasta, CancellationToken cancellationToken)
            => Ok(await _horas.PresupuestoAsync(desde, hasta, cancellationToken));
    }
}
