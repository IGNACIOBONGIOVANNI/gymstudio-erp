using Microsoft.AspNetCore.Authorization;
using EstudioContable.API.Auth;
using System;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace EstudioContable.API.Controllers
{
    [ApiController]
    [Route("api/tesoreria")]
    public class TesoreriaController : ControllerBase
    {
        private readonly ITesoreriaService _tesoreria;

        public TesoreriaController(ITesoreriaService tesoreria)
        {
            _tesoreria = tesoreria;
        }

        public record TransferenciaRequest(string Origen, DateTime Fecha, decimal Importe, string? Detalle);

        /// <summary>Transferencia de fondos entre Caja y Banco (en cualquier sentido).</summary>
        [Authorize(Roles = Roles.Gestion)]
        [HttpPost("transferencias")]
        public async Task<IActionResult> Transferir([FromBody] TransferenciaRequest request, CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<LibroTesoreria>(request.Origen, ignoreCase: true, out var origen) || !Enum.IsDefined(origen) || int.TryParse(request.Origen, out _))
                throw new ArgumentException("El origen de la transferencia debe ser \"Caja\" o \"Banco\".");

            await _tesoreria.TransferirAsync(new DatosTransferencia(origen, request.Fecha, request.Importe, request.Detalle), cancellationToken);
            return StatusCode(201);
        }
    }
}
