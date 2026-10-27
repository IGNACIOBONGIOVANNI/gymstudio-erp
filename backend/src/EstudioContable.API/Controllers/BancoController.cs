using EstudioContable.API.Auth;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Banco.Commands;
using EstudioContable.Application.Banco.Queries.ObtenerLibroBanco;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EstudioContable.API.Controllers
{
    [ApiController]
    [Route("api/banco")]
    public class BancoController : ControllerBase
    {
        private readonly IMediator _mediator;

        public BancoController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Obtiene el Libro Banco del cliente.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? clienteId, CancellationToken cancellationToken)
        {
            var query = new ObtenerLibroBancoQuery(string.IsNullOrWhiteSpace(clienteId) ? "1" : clienteId);
            var resultado = await _mediator.Send(query, cancellationToken);
            return Ok(resultado);
        }

        /// <summary>
        /// Registra un nuevo movimiento bancario.
        /// </summary>
        [Authorize(Roles = Roles.Gestion)]
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CrearMovimientoBancoCommand command, CancellationToken cancellationToken)
        {
            var id = await _mediator.Send(command, cancellationToken);
            return StatusCode(201, new { Id = id });
        }

        /// <summary>
        /// Edita un movimiento de banco existente.
        /// </summary>
        [Authorize(Roles = Roles.Gestion)]
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] EditarMovimientoBancoCommand command, CancellationToken cancellationToken)
        {
            var commandWithId = command with { Id = id };
            await _mediator.Send(commandWithId, cancellationToken);
            return Ok(new { message = "Movimiento bancario actualizado correctamente" });
        }

        /// <summary>
        /// Elimina un movimiento de banco.
        /// </summary>
        [Authorize(Roles = Roles.Gestion)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            var command = new EliminarMovimientoBancoCommand(id);
            await _mediator.Send(command, cancellationToken);
            return Ok(new { message = "Movimiento bancario eliminado correctamente" });
        }
    }
}