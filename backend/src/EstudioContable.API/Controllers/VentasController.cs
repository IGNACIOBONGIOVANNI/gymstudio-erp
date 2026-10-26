using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Ventas.Commands;
using EstudioContable.Application.Ventas.Queries.ExportarLibroVenta;
using EstudioContable.Application.Ventas.Queries.ObtenerLibroVenta;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EstudioContable.API.Controllers
{
    [ApiController]
    [Route("api/ventas")]
    [Authorize]
    public class VentasController : ControllerBase
    {
        private readonly IMediator _mediator;

        public VentasController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Obtiene el Libro Venta con todas las facturas de venta de un cliente.
        /// </summary>
        [HttpGet]
        [AllowAnonymous] // Temporal para probar sin JWT
        public async Task<IActionResult> Get([FromQuery] long? clienteId, CancellationToken cancellationToken)
        {
            var query = new ObtenerLibroVentaQuery(clienteId);
            var resultado = await _mediator.Send(query, cancellationToken);
            return Ok(resultado);
        }

        /// <summary>
        /// Exporta el Libro Venta a un archivo Excel.
        /// </summary>
        [HttpGet("export")]
        [AllowAnonymous] // Temporal para desarrollo local sin JWT
        public async Task<IActionResult> Exportar([FromQuery] long? clienteId, CancellationToken cancellationToken)
        {
            var fileBytes = await _mediator.Send(new ExportarLibroVentaQuery(clienteId), cancellationToken);
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"LibroVenta_{clienteId}.xlsx");
        }

        /// <summary>
        /// Importa facturas de venta desde un archivo Excel masivamente.
        /// </summary>
        [HttpPost("import")]
        [AllowAnonymous] // Temporal para desarrollo local sin JWT
        public async Task<IActionResult> Importar([FromQuery] long? clienteId, IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No se proporcionó un archivo válido.");

            using var stream = file.OpenReadStream();
            var cantInsertadas = await _mediator.Send(new ImportarLibroVentaCommand(clienteId, stream), cancellationToken);
            
            return Ok(new { FacturasImportadas = cantInsertadas });
        }

        /// <summary>
        /// Crea una factura de venta manual.
        /// </summary>
        [HttpPost]
        [AllowAnonymous] // Temporal para probar sin JWT
        public async Task<IActionResult> Post([FromBody] CrearFacturaVentaCommand command, CancellationToken cancellationToken)
        {
            var id = await _mediator.Send(command, cancellationToken);
            return Ok(new { Id = id });
        }
    }
}
