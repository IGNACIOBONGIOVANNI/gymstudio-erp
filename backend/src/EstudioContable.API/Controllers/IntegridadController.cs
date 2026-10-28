using System.Threading;
using System.Threading.Tasks;
using EstudioContable.API.Auth;
using EstudioContable.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EstudioContable.API.Controllers
{
    /// <summary>
    /// Control de integridad contable: el sistema se audita a sí mismo cruzando facturas, libros, stock y Mayor.
    /// Es de solo lectura y solo lo ven Administrador y Contador.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = Roles.Gestion)]
    public class IntegridadController : ControllerBase
    {
        private readonly IControlIntegridadService _integridad;

        public IntegridadController(IControlIntegridadService integridad) => _integridad = integridad;

        [HttpGet]
        public async Task<IActionResult> Controlar(CancellationToken cancellationToken)
        {
            var r = await _integridad.EjecutarAsync(cancellationToken);
            return Ok(new { r.Fecha, r.Total, r.Correctos, TodoCorrecto = r.Correctos == r.Total, r.Controles });
        }
    }
}
