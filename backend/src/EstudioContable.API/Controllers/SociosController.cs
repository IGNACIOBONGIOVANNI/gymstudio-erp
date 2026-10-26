using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Gimnasio.Socios.Commands;
using EstudioContable.Application.Gimnasio.Socios.Queries;
using EstudioContable.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EstudioContable.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SociosController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IAppDbContext _context;

        public SociosController(IMediator mediator, IAppDbContext context)
        {
            _mediator = mediator;
            _context = context;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetSocios(CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new ObtenerSociosQuery(), cancellationToken);
            return Ok(result);
        }

        [HttpGet("estado")]
        [AllowAnonymous]
        public async Task<IActionResult> GetEstadoSocios(CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new ObtenerEstadoSociosQuery(), cancellationToken);
            return Ok(result);
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> CrearSocio([FromBody] CrearSocioCommand command, CancellationToken cancellationToken)
        {
            var id = await _mediator.Send(command, cancellationToken);
            return Ok(new { Id = id });
        }

        public record ActualizarSocioRequest(string Nombre, string Apellido, string Dni, string Telefono);

        [HttpPut("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> ActualizarSocio(long id, [FromBody] ActualizarSocioRequest request, CancellationToken cancellationToken)
        {
            var command = new ActualizarSocioCommand(id, request.Nombre, request.Apellido, request.Dni, request.Telefono);
            var existe = await _mediator.Send(command, cancellationToken);
            return existe ? NoContent() : NotFound();
        }

        [HttpDelete("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> EliminarSocio(long id, CancellationToken cancellationToken)
        {
            var socio = await _context.Socios.FindAsync(new object[] { id }, cancellationToken);
            if (socio == null) return NotFound();

            _context.Socios.Remove(socio);
            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
    }
}
