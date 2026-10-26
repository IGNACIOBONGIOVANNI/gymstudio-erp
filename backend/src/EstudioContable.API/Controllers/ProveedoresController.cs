using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EstudioContable.Domain.Entities;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

namespace EstudioContable.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProveedoresController : ControllerBase
    {
        private readonly EstudioContable.Application.Interfaces.IAppDbContext _context;
        public ProveedoresController(EstudioContable.Application.Interfaces.IAppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var proveedores = await _context.Proveedores
                .Where(p => p.Activo)
                .ToListAsync(cancellationToken);
            return Ok(proveedores);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Proveedor proveedor, CancellationToken cancellationToken)
        {
            _context.Proveedores.Add(proveedor);
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(proveedor);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, [FromBody] Proveedor proveedor, CancellationToken cancellationToken)
        {
            var dbProveedor = await _context.Proveedores.FindAsync(new object[] { id }, cancellationToken);
            if (dbProveedor == null) return NotFound();

            dbProveedor.RazonSocial = proveedor.RazonSocial;
            dbProveedor.Direccion = proveedor.Direccion;
            dbProveedor.Localidad = proveedor.Localidad;
            dbProveedor.Cuit = proveedor.Cuit;
            dbProveedor.CondicionIva = proveedor.CondicionIva;

            await _context.SaveChangesAsync(cancellationToken);
            return Ok(dbProveedor);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
        {
            var dbProveedor = await _context.Proveedores.FindAsync(new object[] { id }, cancellationToken);
            if (dbProveedor == null) return NotFound();

            dbProveedor.Activo = false;
            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
    }
}
