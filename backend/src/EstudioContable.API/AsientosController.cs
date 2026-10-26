using System.Security.Claims;
using EstudioContable.Application.Asientos.Commands.ConfirmarAsiento;
using EstudioContable.Application.Asientos.Commands.CrearAsiento; // <-- Agregamos la ruta al comando de creación
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.API.Controllers
{
    [ApiController]
    [Route("api/asientos")]
    [Authorize] // requiere estar autenticado; los endpoints afinan el rol individualmente
    public class AsientosController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AsientosController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Obtiene un listado de asientos contables.
        /// </summary>
        [HttpGet]
        [AllowAnonymous] // Temporal para probar sin JWT
        public async Task<IActionResult> Get([FromQuery] long? clienteId, CancellationToken cancellationToken)
        {
            var query = new EstudioContable.Application.Asientos.Queries.ObtenerAsientos.ObtenerAsientosQuery(clienteId);
            var resultado = await _mediator.Send(query, cancellationToken);
            return Ok(resultado);
        }

        /// <summary>
        /// Obtiene el Libro Mayor agrupando saldos por cuenta contable.
        /// </summary>
        [HttpGet("mayor")]
        [AllowAnonymous] // Temporal para probar sin JWT
        public async Task<IActionResult> GetMayor([FromQuery] long clienteId, CancellationToken cancellationToken)
        {
            var query = new EstudioContable.Application.Asientos.Queries.ObtenerSaldosMayor.ObtenerSaldosMayorQuery(clienteId);
            var resultado = await _mediator.Send(query, cancellationToken);
            return Ok(resultado);
        }

        /// <summary>
        /// Obtiene un asiento contable específico por su Id (incluyendo sus líneas).
        /// </summary>
        [HttpGet("{id:long}")]
        [AllowAnonymous] // Temporal para probar sin JWT
        public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
        {
            var query = new EstudioContable.Application.Asientos.Queries.ObtenerAsientoPorId.ObtenerAsientoPorIdQuery(id);
            var resultado = await _mediator.Send(query, cancellationToken);

            if (resultado == null)
                return NotFound();

            return Ok(resultado);
        }

        /// <summary>
        /// Crea un nuevo asiento contable desde cero.
        /// </summary>
        [HttpPost]
        [AllowAnonymous] // <-- Permite probarlo en Swagger sin tener que hacer un login previo
        public async Task<IActionResult> Crear([FromBody] CrearAsientoCommand command, CancellationToken cancellationToken)
        {
            var resultado = await _mediator.Send(command, cancellationToken);
            return Ok(resultado);
        }

        /// <summary>
        /// Confirma un asiento en estado Borrador, validando la partida doble.
        /// Sólo Administrador o Contador pueden confirmar — un usuario Cliente
        /// tiene acceso de sólo lectura a sus propios asientos, nunca puede confirmarlos.
        /// </summary>
        /// <response code="200">Asiento confirmado correctamente.</response>
        /// <response code="404">El asiento no existe.</response>
        /// <response code="422">El asiento no está balanceado (Debe != Haber).</response>
        [HttpPost("{id:long}/confirmar")]
        [AllowAnonymous] // Temporal para probar sin JWT
        [ProducesResponseType(typeof(ConfirmarAsientoResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<ConfirmarAsientoResult>> Confirmar(long id, CancellationToken cancellationToken)
        {
            // Hardcodeado temporalmente para pruebas en Swagger sin autenticación JWT
            var usuarioId = 1L;

            var command = new ConfirmarAsientoCommand(id, usuarioId);
            var resultado = await _mediator.Send(command, cancellationToken);

            // Cualquier excepción (NotFound, AsientoDesbalanceado, etc.) se propaga
            // sin try/catch aquí: la captura y el mapeo a HTTP los resuelve
            // ExceptionMiddleware de forma centralizada.
            return Ok(resultado);
        }

        /// <summary>
        /// Obtiene la Factura (comprobante) asociada a un Asiento Contable
        /// </summary>
        [HttpGet("{id:long}/factura")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFacturaByAsientoId(long id, [FromServices] EstudioContable.Application.Interfaces.IAppDbContext context, CancellationToken cancellationToken)
        {
            var factura = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                context.Facturas.Include(f => f.Items).Include(f => f.ClienteObj).Include(f => f.ProveedorObj).Include(f => f.SocioObj), 
                f => f.AsientoContableId == id, 
                cancellationToken);

            if (factura == null) 
                return NotFound(new { Mensaje = "No hay factura o comprobante asociado a este asiento." });

            string tipoFactura = "C"; // Por defecto (Consumidor final o Monotributo)
            if (!string.IsNullOrEmpty(factura.TipoComprobante))
            {
                var tipoUpper = factura.TipoComprobante.ToUpper();
                if (tipoUpper.Contains("A")) tipoFactura = "A";
                else if (tipoUpper.Contains("B")) tipoFactura = "B";
                else if (tipoUpper.Contains("C")) tipoFactura = "C";
            }

            return Ok(new {
                id = factura.Id,
                clienteNombre = factura.SocioObj != null ? $"{factura.SocioObj.Nombre} {factura.SocioObj.Apellido}" : (factura.ClienteObj != null ? factura.ClienteObj.RazonSocial : factura.Cliente),
                cuit = factura.SocioObj != null ? factura.SocioObj.Dni : (factura.ClienteObj != null ? factura.ClienteObj.Cuit : (factura.ProveedorObj != null ? factura.ProveedorObj.Cuit : "Consumidor Final")),
                tipo = tipoFactura,
                cliente = factura.ClienteObj == null ? null : new {
                    razonSocial = factura.ClienteObj.RazonSocial,
                    direccion = factura.ClienteObj.Direccion,
                    localidad = factura.ClienteObj.Localidad,
                    cuit = factura.ClienteObj.Cuit,
                    condicionIva = factura.ClienteObj.CondicionIva
                },
                socio = factura.SocioObj == null ? null : new {
                    nombre = factura.SocioObj.Nombre,
                    apellido = factura.SocioObj.Apellido,
                    dni = factura.SocioObj.Dni,
                    telefono = factura.SocioObj.Telefono
                },
                proveedor = factura.ProveedorObj == null ? null : new {
                    razonSocial = factura.ProveedorObj.RazonSocial,
                    direccion = factura.ProveedorObj.Direccion,
                    localidad = factura.ProveedorObj.Localidad,
                    cuit = factura.ProveedorObj.Cuit,
                    condicionIva = factura.ProveedorObj.CondicionIva
                },
                fecha = factura.Fecha,
                total = factura.Total,
                cae = factura.Cae,
                fechaVencimientoCae = factura.FechaVencimientoCae,
                items = factura.Items.Select(i => new {
                    descripcion = i.Descripcion,
                    cantidad = i.Cantidad,
                    precioUnitario = i.PrecioUnitario,
                    subtotal = i.Subtotal
                })
            });
        }

        private long ObtenerUsuarioIdAutenticado()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(claim) || !long.TryParse(claim, out var usuarioId))
                throw new UnauthorizedAccessException("El token no contiene un identificador de usuario válido.");

            return usuarioId;
        }

        /// <summary>
        /// Elimina un asiento contable.
        /// </summary>
        [HttpDelete("{id:long}")]
        [AllowAnonymous] // Temporal para probar sin JWT
        public async Task<IActionResult> Eliminar(long id, CancellationToken cancellationToken)
        {
            var command = new EstudioContable.Application.Asientos.Commands.EliminarAsiento.EliminarAsientoCommand(id);
            await _mediator.Send(command, cancellationToken);
            return NoContent();
        }
    }
}


