using EstudioContable.API.Auth;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Caja.Commands;
using EstudioContable.Application.Caja.Queries.ObtenerLibroCaja;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EstudioContable.API.Controllers
{
    [ApiController]
    [Route("api/caja")]
    public class CajaController : ControllerBase
    {
        private readonly IMediator _mediator;

        public CajaController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Obtiene el Libro Caja del cliente.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? clienteId, CancellationToken cancellationToken)
        {
            var query = new ObtenerLibroCajaQuery(string.IsNullOrWhiteSpace(clienteId) ? "1" : clienteId);
            var resultado = await _mediator.Send(query, cancellationToken);
            return Ok(resultado);
        }

        /// <summary>
        /// Registra un nuevo movimiento de caja.
        /// </summary>
        [Authorize(Roles = Roles.Gestion)]
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CrearMovimientoCajaCommand command, CancellationToken cancellationToken)
        {
            var id = await _mediator.Send(command, cancellationToken);
            return StatusCode(201, new { Id = id });
        }

        /// <summary>
        /// Edita un movimiento de caja existente.
        /// </summary>
        [Authorize(Roles = Roles.Gestion)]
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] EditarMovimientoCajaCommand command, CancellationToken cancellationToken)
        {
            // Le asignamos el ID de la URL al comando
            var commandWithId = command with { Id = id };
            await _mediator.Send(commandWithId, cancellationToken);
            return Ok(new { message = "Movimiento actualizado correctamente" });
        }

        /// <summary>
        /// Elimina un movimiento de caja.
        /// </summary>
        [Authorize(Roles = Roles.Gestion)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            var command = new EliminarMovimientoCajaCommand(id);
            await _mediator.Send(command, cancellationToken);
            return Ok(new { message = "Movimiento eliminado correctamente" });
        }
    }
}