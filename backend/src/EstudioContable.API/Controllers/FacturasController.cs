using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
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

            var factura = new Factura
            {
                Fecha = dto.Fecha,
                TipoOperacion = dto.TipoOperacion,
                CondicionPago = dto.CondicionPago,
                TipoComprobante = dto.TipoComprobante,
                Cliente = dto.Cliente,
                ClienteComercialId = dto.ClienteId,
                ProveedorId = dto.ProveedorId,
                Subtotal = dto.Subtotal,
                Iva = dto.Iva,
                Total = dto.Total,
                Items = dto.Items.Select(i => new FacturaItem
                {
                    ProductoId = i.ProductoId > 0 ? i.ProductoId : null,
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

        private IQueryable<FacturaDto> ConsultarFacturas()
        {
            return _context.Facturas
                .AsNoTracking()
                .Select(f => new FacturaDto
                {
                    Id = f.Id,
                    Fecha = f.Fecha,
                    TipoOperacion = f.TipoOperacion,
                    CondicionPago = f.CondicionPago,
                    TipoComprobante = f.TipoComprobante,
                    Cliente = f.Cliente,
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
        public async Task<IActionResult> ActualizarFactura(long id, [FromBody] ActualizarFacturaDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var datosNuevos = new Factura
            {
                Fecha = dto.Fecha,
                TipoOperacion = dto.TipoOperacion,
                CondicionPago = dto.CondicionPago,
                TipoComprobante = dto.TipoComprobante,
                Cliente = dto.Cliente,
                Subtotal = dto.Subtotal,
                Iva = dto.Iva,
                Total = dto.Total,
                Items = dto.Items.Select(i => new FacturaItem
                {
                    ProductoId = i.ProductoId > 0 ? i.ProductoId : null,
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
        public async Task<IActionResult> AnularFactura(long id, CancellationToken cancellationToken)
        {
            await _facturacionService.AnularFacturaAsync(id, cancellationToken);
            return NoContent();
        }
    }


    public class ActualizarFacturaDto
    {
        public DateTime Fecha { get; set; }
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
        public string Descripcion { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class CrearFacturaDto
    {
        public DateTime Fecha { get; set; }
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
        public string Descripcion { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class FacturaDto
    {
        public long Id { get; set; }
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
