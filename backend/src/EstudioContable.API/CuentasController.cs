using System.Linq;
using EstudioContable.Application.Cuentas.Commands.CrearCuenta;
using EstudioContable.API.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace EstudioContable.API.Controllers
{
    [ApiController]
    [Route("api/cuentas")]
    public class CuentasController : ControllerBase
    {
        private readonly IMediator _mediator;

        public CuentasController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Plan de cuentas activo del cliente, ordenado por código (para los selectores).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Listar([FromServices] EstudioContable.Domain.Interfaces.ICuentaRepository repository,
            [FromQuery] long clienteId = 1, CancellationToken cancellationToken = default)
        {
            var cuentas = await repository.ObtenerTodasAsync(clienteId, cancellationToken);
            return Ok(cuentas
                .Where(c => c.Activa)
                .OrderBy(c => c.Codigo)
                .Select(c => new { id = c.Id.ToString(), codigo = c.Codigo, nombre = c.Nombre }));
        }

        /// <summary>
        /// Registra una nueva cuenta contable.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = Roles.Gestion)]
        public async Task<IActionResult> Crear([FromBody] CrearCuentaCommand command, CancellationToken cancellationToken)
        {
            var resultado = await _mediator.Send(command, cancellationToken);
            return Ok(resultado);
        }
    }
}
