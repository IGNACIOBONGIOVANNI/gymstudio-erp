using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.API.Auth;
using EstudioContable.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using EstudioContable.Domain.Entities;
using EstudioContable.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FacturasController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IFacturacionService _facturacionService;

        public FacturasController(AppDbContext context, IFacturacionService facturacionService)
        {
            _context = context;
            _facturacionService = facturacionService;
        }

        [HttpPost]
        public async Task<IActionResult> CrearFactura([FromBody] CrearFacturaDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await ValidarTipoParaElClienteAsync(dto.TipoComprobante, dto.TipoOperacion, dto.ClienteId, cancellationToken);

            var factura = new Factura
            {
                Fecha = dto.Fecha,
                TipoOperacion = dto.TipoOperacion,
                CondicionPago = dto.CondicionPago,
                TipoComprobante = dto.TipoComprobante,
                Cliente = await NombreDelTerceroAsync(dto.Cliente, dto.TipoOperacion, dto.ClienteId, dto.ProveedorId, cancellationToken),
                ClienteComercialId = dto.TipoOperacion == "Venta" ? dto.ClienteId : null,
                ProveedorId = dto.TipoOperacion == "Compra" ? dto.ProveedorId : null,
                PuntoVenta = dto.PuntoVenta,
                NumeroComprobante = dto.NumeroComprobante,
                Subtotal = dto.Subtotal,
                Iva = dto.Iva,
                Total = dto.Total,
                Items = dto.Items.Select(i => new FacturaItem
                {
                    ProductoId = i.ProductoId > 0 ? i.ProductoId : null,
                    AgregarAlInventario = i.AgregarAlInventario,
                    Descripcion = i.Descripcion,
                    Cantidad = i.Cantidad,
                    PrecioUnitario = i.PrecioUnitario,
                    Subtotal = i.Subtotal
                }).ToList()
            };

            // Asiento, caja/banco y stock los genera el servicio dentro de una transacción.
            await _facturacionService.CrearFacturaAsync(factura, cancellationToken);

            var resultado = await ConsultarFacturas()
                .FirstAsync(f => f.Id == factura.Id, cancellationToken);

            return Created($"/api/facturas/{factura.Id}", resultado);
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerFacturas(CancellationToken cancellationToken)
        {
            var facturas = await ConsultarFacturas().ToListAsync(cancellationToken);
            return Ok(facturas);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> ObtenerFactura(long id, CancellationToken cancellationToken)
        {
            var factura = await ConsultarFacturas().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
            return factura == null ? NotFound(new { Mensaje = "La factura no existe." }) : Ok(factura);
        }

        /// <summary>
        /// Nombre que se imprime en la factura: el que viene en el formulario o, si está
        /// vacío, la razón social del cliente (venta) o proveedor (compra) elegido.
        /// </summary>
        // La factura A se emite a Responsables Inscriptos (y Monotributistas); a un Consumidor Final o
        // un Exento le corresponde la B.
        private async Task ValidarTipoParaElClienteAsync(string? tipoComprobante, string tipoOperacion, long? clienteId, CancellationToken cancellationToken)
        {
            if (tipoOperacion != "Venta" || clienteId is not long id || !string.Equals(tipoComprobante?.Trim(), "A", StringComparison.OrdinalIgnoreCase))
                return;

            var cliente = await _context.ClientesComerciales.AsNoTracking().Where(c => c.Id == id)
                .Select(c => new { c.RazonSocial, c.CondicionIva }).FirstOrDefaultAsync(cancellationToken);
            if (cliente is not null && cliente.CondicionIva != "Responsable Inscripto" && cliente.CondicionIva != "Monotributo")
                throw new ArgumentException($"No se puede emitir una factura A a {cliente.RazonSocial}: es {cliente.CondicionIva}. Corresponde la factura B.");
        }

        private async Task<string> NombreDelTerceroAsync(string? nombre, string tipoOperacion, long? clienteId, long? proveedorId, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(nombre))
                return nombre.Trim();

            if (tipoOperacion == "Venta" && clienteId is long cId)
                return await _context.ClientesComerciales.Where(c => c.Id == cId).Select(c => c.RazonSocial).FirstOrDefaultAsync(cancellationToken)
                    ?? throw new EstudioContable.Application.Common.Exceptions.NotFoundException("Cliente", cId);

            if (tipoOperacion == "Compra" && proveedorId is long pId)
                return await _context.Proveedores.Where(p => p.Id == pId).Select(p => p.RazonSocial).FirstOrDefaultAsync(cancellationToken)
                    ?? throw new EstudioContable.Application.Common.Exceptions.NotFoundException("Proveedor", pId);

            return string.Empty; // el servicio de facturación informa que falta el cliente/proveedor
        }

        private IQueryable<FacturaDto> ConsultarFacturas()
        {
            // Orden de libro: por fecha y, dentro del día, por comprobante.
            return _context.Facturas
                .AsNoTracking()
                .OrderBy(f => f.Fecha).ThenBy(f => f.TipoComprobante).ThenBy(f => f.PuntoVenta)
                .ThenBy(f => f.NumeroComprobante).ThenBy(f => f.Id)
                .Select(f => new FacturaDto
                {
                    Id = f.Id,
                    Fecha = f.Fecha,
                    TipoOperacion = f.TipoOperacion,
                    CondicionPago = f.CondicionPago,
                    TipoComprobante = f.TipoComprobante,
                    PuntoVenta = f.PuntoVenta,
                    NumeroComprobante = f.NumeroComprobante,
                    Cliente = f.Cliente,
                    // CUIT del cliente o proveedor (o el DNI del socio en las cuotas).
                    Cuit = f.ClienteObj != null ? f.ClienteObj.Cuit
                        : f.ProveedorObj != null ? f.ProveedorObj.Cuit
                        : f.SocioObj != null ? f.SocioObj.Dni : null,
                    Subtotal = f.Subtotal,
                    Iva = f.Iva,
                    Total = f.Total,
                    Cae = f.Cae,
                    FechaVencimientoCae = f.FechaVencimientoCae,
                    Anulada = f.Anulada,
                    FechaAnulacion = f.FechaAnulacion,
                    ClienteObj = f.ClienteObj,
                    SocioObj = f.SocioObj,
                    Proveedor = f.ProveedorObj,
                    Items = f.Items.Select(i => new FacturaItemDto
                    {
                        Id = i.Id,
                        ProductoId = i.ProductoId,
                        Descripcion = i.Descripcion,
                        Cantidad = i.Cantidad,
                        PrecioUnitario = i.PrecioUnitario,
                        Subtotal = i.Subtotal
                    }).ToList()
                });
        }

        /// <summary>
        /// Modifica una factura. No reescribe la historia: revierte los efectos de la
        /// versión anterior (contraasiento, tesorería y stock inversos) y aplica los nuevos.
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = Roles.Gestion)]
        public async Task<IActionResult> ActualizarFactura(long id, [FromBody] ActualizarFacturaDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Si no viene el nombre (facturas cargadas sin él), se conserva el del
            // cliente o proveedor que ya tiene asociado la factura.
            var actual = await _context.Facturas.AsNoTracking()
                .Where(f => f.Id == id)
                .Select(f => new { f.ClienteComercialId, f.ProveedorId })
                .FirstOrDefaultAsync(cancellationToken);
            var nombre = actual == null
                ? dto.Cliente
                : await NombreDelTerceroAsync(dto.Cliente, dto.TipoOperacion, actual.ClienteComercialId, actual.ProveedorId, cancellationToken);
            if (actual != null)
                await ValidarTipoParaElClienteAsync(dto.TipoComprobante, dto.TipoOperacion, actual.ClienteComercialId, cancellationToken);

            var datosNuevos = new Factura
            {
                Fecha = dto.Fecha,
                TipoOperacion = dto.TipoOperacion,
                CondicionPago = dto.CondicionPago,
                TipoComprobante = dto.TipoComprobante,
                Cliente = nombre,
                PuntoVenta = dto.PuntoVenta,
                NumeroComprobante = dto.NumeroComprobante,
                Subtotal = dto.Subtotal,
                Iva = dto.Iva,
                Total = dto.Total,
                Items = dto.Items.Select(i => new FacturaItem
                {
                    ProductoId = i.ProductoId > 0 ? i.ProductoId : null,
                    AgregarAlInventario = i.AgregarAlInventario,
                    Descripcion = i.Descripcion,
                    Cantidad = i.Cantidad,
                    PrecioUnitario = i.PrecioUnitario,
                    Subtotal = i.Subtotal
                }).ToList()
            };

            await _facturacionService.ReemplazarFacturaAsync(id, datosNuevos, cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Anula una factura: queda registrada como anulada y sus efectos se revierten
        /// con movimientos inversos. Nunca se borra (trazabilidad contable y fiscal).
        /// </summary>
        [HttpPost("{id}/anular")]
        [Authorize(Roles = Roles.Gestion)]
        public async Task<IActionResult> AnularFactura(long id, CancellationToken cancellationToken)
        {
            await _facturacionService.AnularFacturaAsync(id, cancellationToken);
            return NoContent();
        }
    }


    public class ActualizarFacturaDto
    {
        public DateTime Fecha { get; set; }
        public int? PuntoVenta { get; set; }
        public long? NumeroComprobante { get; set; }
        public string TipoOperacion { get; set; } = string.Empty;
        public string CondicionPago { get; set; } = string.Empty;
        public string TipoComprobante { get; set; } = string.Empty;
        public string Cliente { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public decimal Total { get; set; }
        public System.Collections.Generic.List<ActualizarFacturaItemDto> Items { get; set; } = new();
    }

    public class ActualizarFacturaItemDto
    {
        public long ProductoId { get; set; }
        public bool AgregarAlInventario { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class CrearFacturaDto
    {
        public DateTime Fecha { get; set; }
        public int? PuntoVenta { get; set; }
        public long? NumeroComprobante { get; set; }
        public string TipoOperacion { get; set; } = string.Empty;
        public string CondicionPago { get; set; } = string.Empty;
        public string TipoComprobante { get; set; } = string.Empty;
        public string Cliente { get; set; } = string.Empty;
        public long? ClienteId { get; set; }
        public long? ProveedorId { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public decimal Total { get; set; }
        public System.Collections.Generic.List<CrearFacturaItemDto> Items { get; set; } = new();
    }

    public class CrearFacturaItemDto
    {
        public long ProductoId { get; set; }
        public bool AgregarAlInventario { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class FacturaDto
    {
        public long Id { get; set; }
        public int? PuntoVenta { get; set; }
        public long? NumeroComprobante { get; set; }
        public string? Cuit { get; set; }

        // "0003-00001234" (null en facturas cargadas antes de existir la numeración).
        public string? NumeroFactura => PuntoVenta.HasValue && NumeroComprobante.HasValue
            ? $"{PuntoVenta.Value:D4}-{NumeroComprobante.Value:D8}"
            : null;
        public DateTime Fecha { get; set; }
        public string TipoOperacion { get; set; } = string.Empty;
        public string CondicionPago { get; set; } = string.Empty;
        public string TipoComprobante { get; set; } = string.Empty;
        public string Cliente { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public decimal Total { get; set; }
        public string? Cae { get; set; }
        public DateTime? FechaVencimientoCae { get; set; }
        public bool Anulada { get; set; }
        public DateTime? FechaAnulacion { get; set; }
        public ClienteComercial? ClienteObj { get; set; }
        public Proveedor? Proveedor { get; set; }
        public EstudioContable.Domain.Entities.Socio? SocioObj { get; set; }
        public System.Collections.Generic.List<FacturaItemDto> Items { get; set; } = new();
    }

    public class FacturaItemDto
    {
        public long Id { get; set; }
        public long? ProductoId { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }
}
