using EstudioContable.API.Auth;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Compras.Queries.ObtenerLibroCompra;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EstudioContable.API.Controllers
{
    [ApiController]
    [Route("api/compras")]
    public class ComprasController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ComprasController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Obtiene el Libro Compra con todas las facturas de compra de un cliente.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] long? clienteId, CancellationToken cancellationToken)
        {
            var query = new ObtenerLibroCompraQuery(clienteId);
            var resultado = await _mediator.Send(query, cancellationToken);
            return Ok(resultado);
        }

        /// <summary>
        /// Crea una factura de compra manual.
        /// </summary>
        [Authorize(Roles = Roles.Gestion)]
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] EstudioContable.Application.Compras.Commands.CrearFacturaCompraCommand command, CancellationToken cancellationToken)
        {
            var id = await _mediator.Send(command, cancellationToken);
            return Ok(new { Id = id });
        }
        /// <summary>
        /// Elimina una factura de compra y sus movimientos asociados.
        /// </summary>
        [Authorize(Roles = Roles.Gestion)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
        {
            var command = new EstudioContable.Application.Compras.Commands.EliminarFacturaCompraCommand(id);
            await _mediator.Send(command, cancellationToken);
            return Ok(new { message = "Factura de compra eliminada correctamente" });
        }
    }
}
