using System.Collections.Generic;
using System.Threading.Tasks;
using EstudioContable.Application.Inventario.Queries.DTOs;
using EstudioContable.Application.Inventario.Queries.ObtenerProductos;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EstudioContable.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductosController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ProductosController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<ActionResult<List<ProductoDto>>> Get()
        {
            var query = new ObtenerProductosQuery();
            var productos = await _mediator.Send(query);
            return Ok(productos);
        }

        [HttpPost]
        public async Task<ActionResult<long>> Post([FromBody] EstudioContable.Application.Inventario.Commands.CrearProducto.CrearProductoCommand command)
        {
            var id = await _mediator.Send(command);
            return CreatedAtAction(nameof(Get), new { id }, id);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult> Put(long id, [FromBody] EstudioContable.Application.Inventario.Commands.ActualizarProducto.ActualizarProductoCommand command)
        {
            if (id != command.Id) return BadRequest("ID mismatch");
            
            var result = await _mediator.Send(command);
            if (!result) return NotFound();
            
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(long id)
        {
            var command = new EstudioContable.Application.Inventario.Commands.EliminarProducto.EliminarProductoCommand(id);
            var result = await _mediator.Send(command);
            if (!result) return NotFound();

            return NoContent();
        }
    }
}
