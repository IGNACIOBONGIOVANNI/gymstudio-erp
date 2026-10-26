using System;
using System.Linq;
using System.Threading.Tasks;
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

        public FacturasController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CrearFactura([FromBody] CrearFacturaDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {

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
                    Descripcion = i.Descripcion,
                    Cantidad = i.Cantidad,
                    PrecioUnitario = i.PrecioUnitario,
                    Subtotal = i.Subtotal
                }).ToList()
            };

            _context.Facturas.Add(factura);
            await _context.SaveChangesAsync();

            if (dto.CondicionPago == "TransferenciaBanco")
            {
                var tipoMov = dto.TipoOperacion == "Venta" ? EstudioContable.Domain.Enums.TipoMovimientoBanco.Ingreso : EstudioContable.Domain.Enums.TipoMovimientoBanco.Egreso;
                var mov = new MovimientoBanco("1", dto.Fecha, $"Factura de {dto.TipoOperacion} N° {factura.Id} - {dto.Cliente}", tipoMov, dto.Total, factura.Id);
                _context.MovimientosBanco.Add(mov);
                await _context.SaveChangesAsync();
            }
            else if (dto.CondicionPago == "ContadoCaja")
            {
                var tipoMov = dto.TipoOperacion == "Venta" ? EstudioContable.Domain.Enums.TipoMovimientoCaja.Ingreso : EstudioContable.Domain.Enums.TipoMovimientoCaja.Egreso;
                var mov = new MovimientoCaja("1", dto.Fecha, $"Factura de {dto.TipoOperacion} N° {factura.Id} - {dto.Cliente}", tipoMov, dto.Total, factura.Id);
                _context.MovimientosCaja.Add(mov);
                await _context.SaveChangesAsync();
            }

            // Actualizar Stock
            foreach (var itemDto in dto.Items)
            {
                if (itemDto.ProductoId > 0)
                {
                    var producto = await _context.Productos.FindAsync(itemDto.ProductoId);
                    if (producto != null)
                    {
                        int cantidadFactor = dto.TipoOperacion == "Venta" ? -itemDto.Cantidad : itemDto.Cantidad;
                        producto.ActualizarStock(cantidadFactor);
                        
                        var movStock = new MovimientoStock(
                            producto.Id,
                            new DateTimeOffset(dto.Fecha.ToUniversalTime()),
                            $"Factura de {dto.TipoOperacion} N° {factura.Id} - {dto.Cliente}",
                            dto.TipoOperacion == "Compra" ? itemDto.Cantidad : 0,
                            dto.TipoOperacion == "Venta" ? itemDto.Cantidad : 0,
                            producto.StockActual
                        );
                        _context.MovimientosStock.Add(movStock);
                    }
                }
            }
            await _context.SaveChangesAsync();

            // Generar Asiento Contable
            var maxNumero = await _context.AsientosContables.MaxAsync(a => (long?)a.NumeroAsiento) ?? 0;
            var numeroAsiento = maxNumero + 1;
            var asiento = new AsientoContable(
                1L, // ClienteId por defecto
                numeroAsiento,
                DateOnly.FromDateTime(dto.Fecha),
                $"Factura de {dto.TipoOperacion} N° {factura.Id} - {dto.Cliente}",
                1L // UsuarioCreadorId por defecto
            );

            if (dto.TipoOperacion == "Venta")
            {
                long cuentaIdDebe = dto.CondicionPago switch
                {
                    "ContadoCaja" => 101L,
                    "TransferenciaBanco" => 102L,
                    _ => 103L
                };
                asiento.AgregarLinea(cuentaIdDebe, Math.Round(dto.Total, 2), 0, "Total Factura");
                asiento.AgregarLinea(104L, 0, Math.Round(dto.Subtotal, 2), "Ventas");
                if (dto.Iva > 0)
                {
                    asiento.AgregarLinea(201L, 0, Math.Round(dto.Iva, 2), "IVA Débito Fiscal");
                }
            }
            else // Compra
            {
                long cuentaIdHaber = dto.CondicionPago switch
                {
                    "ContadoCaja" => 101L,
                    "TransferenciaBanco" => 102L,
                    _ => 202L
                };
                asiento.AgregarLinea(501L, Math.Round(dto.Subtotal, 2), 0, "Compras");
                if (dto.Iva > 0)
                {
                    asiento.AgregarLinea(105L, Math.Round(dto.Iva, 2), 0, "IVA Crédito Fiscal");
                }
                asiento.AgregarLinea(cuentaIdHaber, 0, Math.Round(dto.Total, 2), "Total Factura");
            }

            asiento.Confirmar(1L);
            _context.AsientosContables.Add(asiento);
            await _context.SaveChangesAsync();

            factura.AsientoContableId = asiento.Id;
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            var facturaCompleta = await _context.Facturas
                .Include(f => f.ClienteObj)
                .Include(f => f.ProveedorObj)
                  .Include(f => f.SocioObj)
                .Include(f => f.Items)
                .FirstOrDefaultAsync(f => f.Id == factura.Id);

            var resultDto = new FacturaDto
            {
                Id = facturaCompleta.Id,
                Fecha = facturaCompleta.Fecha,
                TipoOperacion = facturaCompleta.TipoOperacion,
                CondicionPago = facturaCompleta.CondicionPago,
                TipoComprobante = facturaCompleta.TipoComprobante,
                Cliente = facturaCompleta.Cliente,
                Subtotal = facturaCompleta.Subtotal,
                Iva = facturaCompleta.Iva,
                Total = facturaCompleta.Total,
                Cae = facturaCompleta.Cae,
                FechaVencimientoCae = facturaCompleta.FechaVencimientoCae,
                ClienteObj = facturaCompleta.ClienteObj,
                  SocioObj = facturaCompleta.SocioObj,
                Proveedor = facturaCompleta.ProveedorObj,
                Items = facturaCompleta.Items.Select(i => new FacturaItemDto
                {
                    Id = i.Id,
                    Descripcion = i.Descripcion,
                    Cantidad = i.Cantidad,
                    PrecioUnitario = i.PrecioUnitario,
                    Subtotal = i.Subtotal
                }).ToList()
            };

            return Created($"/api/facturas/{factura.Id}", resultDto);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        [HttpGet]
        public async Task<IActionResult> ObtenerFacturas()
        {
            var facturas = await _context.Facturas
                .Include(f => f.Items)
                .Include(f => f.ClienteObj)
                .Include(f => f.ProveedorObj)
                  .Include(f => f.SocioObj)
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
                    ClienteObj = f.ClienteObj,
                      SocioObj = f.SocioObj,
                    Proveedor = f.ProveedorObj,
                    Items = f.Items.Select(i => new FacturaItemDto
                    {
                        Id = i.Id,
                        Descripcion = i.Descripcion,
                        Cantidad = i.Cantidad,
                        PrecioUnitario = i.PrecioUnitario,
                        Subtotal = i.Subtotal
                    }).ToList()
                }).ToListAsync();

            return Ok(facturas);
        }
        
        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarFactura(long id, [FromBody] ActualizarFacturaDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var factura = await _context.Facturas
                .Include(f => f.Items)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (factura == null)
            {
                return NotFound();
            }

            // 1. Revertir movimientos anteriores
            // Buscamos cualquier movimiento asociado a esta factura usando el ID
            var oldMovBanco = await _context.MovimientosBanco
                .FirstOrDefaultAsync(m => m.Detalle.Contains($"N° {id}"));
            if (oldMovBanco != null) _context.MovimientosBanco.Remove(oldMovBanco);

            var oldMovCaja = await _context.MovimientosCaja
                .FirstOrDefaultAsync(m => m.Detalle.Contains($"N° {id}"));
            if (oldMovCaja != null) _context.MovimientosCaja.Remove(oldMovCaja);

            // 2. Actualizar campos de la factura
            factura.Fecha = dto.Fecha;
            factura.TipoOperacion = dto.TipoOperacion;
            factura.CondicionPago = dto.CondicionPago;
            factura.TipoComprobante = dto.TipoComprobante;
            factura.Cliente = dto.Cliente;
            factura.Subtotal = dto.Subtotal;
            factura.Iva = dto.Iva;
            factura.Total = dto.Total;

            // 3. Actualizar items (reemplazo completo para simplificar)
            _context.FacturaItems.RemoveRange(factura.Items);
            factura.Items.Clear();
            
            foreach (var item in dto.Items)
            {
                factura.Items.Add(new FacturaItem
                {
                    Descripcion = item.Descripcion,
                    Cantidad = item.Cantidad,
                    PrecioUnitario = item.PrecioUnitario,
                    Subtotal = item.Subtotal
                });
            }

            // 4. Generar nuevos movimientos según la nueva condición de pago
            if (dto.CondicionPago == "TransferenciaBanco")
            {
                var tipoMov = dto.TipoOperacion == "Venta" ? EstudioContable.Domain.Enums.TipoMovimientoBanco.Ingreso : EstudioContable.Domain.Enums.TipoMovimientoBanco.Egreso;
                var mov = new MovimientoBanco("1", dto.Fecha, $"Factura de {dto.TipoOperacion} N° {factura.Id} - {dto.Cliente}", tipoMov, dto.Total, factura.Id);
                _context.MovimientosBanco.Add(mov);
            }
            else if (dto.CondicionPago == "ContadoCaja")
            {
                var tipoMov = dto.TipoOperacion == "Venta" ? EstudioContable.Domain.Enums.TipoMovimientoCaja.Ingreso : EstudioContable.Domain.Enums.TipoMovimientoCaja.Egreso;
                var mov = new MovimientoCaja("1", dto.Fecha, $"Factura de {dto.TipoOperacion} N° {factura.Id} - {dto.Cliente}", tipoMov, dto.Total, factura.Id);
                _context.MovimientosCaja.Add(mov);
            }

            // 5. Guardar todo
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarFactura(long id)
        {
            // 1. Buscamos la factura en la base de datos por su ID
            var factura = await _context.Facturas.FindAsync(id);
            
            // 2. Si no existe, devolvemos 404
            if (factura == null)
            {
                return NotFound();
            }

            var asiento = await _context.AsientosContables
                .FirstOrDefaultAsync(a => a.Id == factura.AsientoContableId || 
                                         a.Descripcion.Contains($"Factura de Venta N° {factura.Id}") ||
                                         a.Descripcion.Contains($"Factura de Compra N° {factura.Id}"));
            
            if (asiento != null)
            {
                _context.AsientosContables.Remove(asiento);
            }

            var movCaja = await _context.MovimientosCaja.Where(m => m.FacturaId == factura.Id).ToListAsync();
            if (movCaja.Any()) _context.MovimientosCaja.RemoveRange(movCaja);

            var movBanco = await _context.MovimientosBanco.Where(m => m.FacturaId == factura.Id).ToListAsync();
            if (movBanco.Any()) _context.MovimientosBanco.RemoveRange(movBanco);

            _context.Facturas.Remove(factura);
            await _context.SaveChangesAsync();

            // 4. Devolvemos 204 No Content (lo que espera nuestro frontend)
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
        public ClienteComercial? ClienteObj { get; set; }
        public Proveedor? Proveedor { get; set; }
        public EstudioContable.Domain.Entities.Socio? SocioObj { get; set; }
        public System.Collections.Generic.List<FacturaItemDto> Items { get; set; } = new();
    }

    public class FacturaItemDto
    {
        public long Id { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }
}








