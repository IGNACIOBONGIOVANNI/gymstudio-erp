using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Gimnasio.Planes.Commands;
using EstudioContable.Application.Gimnasio.Planes.Queries;
using EstudioContable.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EstudioContable.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlanesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IAppDbContext _context;

        public PlanesController(IMediator mediator, IAppDbContext context)
        {
            _mediator = mediator;
            _context = context;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetPlanes(CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new ObtenerPlanesQuery(), cancellationToken);
            return Ok(result);
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> CrearPlan([FromBody] CrearPlanCommand command, CancellationToken cancellationToken)
        {
            var id = await _mediator.Send(command, cancellationToken);
            return Ok(new { Id = id });
        }

        public record ActualizarPlanRequest(string Nombre, decimal PrecioMensual, int DuracionMeses);

        [HttpPut("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> ActualizarPlan(long id, [FromBody] ActualizarPlanRequest request, CancellationToken cancellationToken)
        {
            var command = new ActualizarPlanCommand(id, request.Nombre, request.PrecioMensual, request.DuracionMeses);
            var existe = await _mediator.Send(command, cancellationToken);
            return existe ? NoContent() : NotFound();
        }

        [HttpDelete("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> EliminarPlan(long id, CancellationToken cancellationToken)
        {
            var plan = await _context.Planes.FindAsync(new object[] { id }, cancellationToken);
            if (plan == null) return NotFound();

            _context.Planes.Remove(plan);
            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
    }
}
