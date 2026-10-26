using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EstudioContable.Domain.Entities;
using System.Threading.Tasks;
using System.Threading;

namespace EstudioContable.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientesController : ControllerBase
    {
        private readonly EstudioContable.Application.Interfaces.IAppDbContext _context;
        public ClientesController(EstudioContable.Application.Interfaces.IAppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var clientes = await _context.ClientesComerciales
                .Where(c => c.Activo)
                .ToListAsync(cancellationToken);
            return Ok(clientes);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ClienteComercial cliente, CancellationToken cancellationToken)
        {
            _context.ClientesComerciales.Add(cliente);
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(cliente);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, [FromBody] ClienteComercial cliente, CancellationToken cancellationToken)
        {
            var dbCliente = await _context.ClientesComerciales.FindAsync(new object[] { id }, cancellationToken);
            if (dbCliente == null) return NotFound();

            dbCliente.RazonSocial = cliente.RazonSocial;
            dbCliente.Direccion = cliente.Direccion;
            dbCliente.Localidad = cliente.Localidad;
            dbCliente.Cuit = cliente.Cuit;
            dbCliente.CondicionIva = cliente.CondicionIva;

            await _context.SaveChangesAsync(cancellationToken);
            return Ok(dbCliente);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
        {
            var dbCliente = await _context.ClientesComerciales.FindAsync(new object[] { id }, cancellationToken);
            if (dbCliente == null) return NotFound();

            dbCliente.Activo = false;
            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
    }
}