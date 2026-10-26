using System.Collections.Generic;
using System.Threading.Tasks;
using EstudioContable.Application.Inventario.Queries.DTOs;
using EstudioContable.Application.Inventario.Queries.ObtenerMovimientosStock;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EstudioContable.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventarioController : ControllerBase
    {
        private readonly IMediator _mediator;

        public InventarioController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("movimientos/{productoId}")]
        public async Task<ActionResult<List<MovimientoStockDto>>> GetMovimientosStock(long productoId)
        {
            var query = new ObtenerMovimientosStockQuery(productoId);
            var movimientos = await _mediator.Send(query);
            return Ok(movimientos);
        }
    }
}
