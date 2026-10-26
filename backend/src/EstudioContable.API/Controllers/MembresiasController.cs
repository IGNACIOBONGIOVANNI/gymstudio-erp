using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Gimnasio.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EstudioContable.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MembresiasController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MembresiasController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("{id}/cobrar")]
        [AllowAnonymous]
        public async Task<IActionResult> Cobrar(long id, [FromBody] CobrarMembresiaCommand command, CancellationToken cancellationToken)
        {
            if (id != command.MembresiaId)
            {
                return BadRequest("El ID de la URL no coincide con el del cuerpo de la petición.");
            }

            try
            {
                var resultado = await _mediator.Send(command, cancellationToken);
                return Ok(new { Exito = resultado });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { Mensaje = ex.Message });
            }
        }

        [HttpPost("asignar-y-cobrar")]
        [AllowAnonymous]
        public async Task<IActionResult> AsignarYCobrar([FromBody] AsignarYCobrarMembresiaCommand command, CancellationToken cancellationToken)
        {
            try
            {
                var id = await _mediator.Send(command, cancellationToken);
                return Ok(new { Id = id });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { Mensaje = ex.Message });
            }
        }
    }
}
