using EstudioContable.Application.Cuentas.Commands.CrearCuenta;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace EstudioContable.API.Controllers
{
    [ApiController]
    [Route("api/cuentas")]
    [Authorize]
    public class CuentasController : ControllerBase
    {
        private readonly IMediator _mediator;

        public CuentasController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Registra una nueva cuenta contable.
        /// </summary>
        [HttpPost]
        [AllowAnonymous] // Permitir pruebas en Swagger sin token
        public async Task<IActionResult> Crear([FromBody] CrearCuentaCommand command, CancellationToken cancellationToken)
        {
            var resultado = await _mediator.Send(command, cancellationToken);
            return Ok(resultado);
        }
    }
}
