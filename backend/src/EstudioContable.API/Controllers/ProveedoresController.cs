using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Comercial;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProveedoresController : ControllerBase
    {
        private readonly IAppDbContext _context;

        public ProveedoresController(IAppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var lista = await _context.Proveedores
                .Where(x => x.Activo)
                .OrderBy(x => x.RazonSocial)
                .ToListAsync(cancellationToken);
            return Ok(lista);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DatosComerciales datos, CancellationToken cancellationToken)
        {
            var validados = datos.Validar();
            await ValidarCuitNoRepetidoAsync(validados.Cuit, idExcluido: null, cancellationToken);

            // Si ya existió y se dio de baja, se reactiva el mismo registro (conserva su
            // historial de facturas) en lugar de crear un duplicado con el mismo CUIT.
            var normalizado = EstudioContable.Domain.Cuit.Normalizar(validados.Cuit);
            var dadoDeBaja = (await _context.Proveedores.Where(x => !x.Activo).ToListAsync(cancellationToken))
                .FirstOrDefault(x => EstudioContable.Domain.Cuit.Normalizar(x.Cuit) == normalizado);

            var entidad = dadoDeBaja ?? new Proveedor();
            Aplicar(entidad, validados);
            entidad.Activo = true;
            if (dadoDeBaja == null)
                _context.Proveedores.Add(entidad);
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(entidad);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, [FromBody] DatosComerciales datos, CancellationToken cancellationToken)
        {
            var entidad = await _context.Proveedores.FirstOrDefaultAsync(x => x.Id == id && x.Activo, cancellationToken);
            if (entidad == null) return NotFound();

            var validados = datos.Validar();
            await ValidarCuitNoRepetidoAsync(validados.Cuit, idExcluido: id, cancellationToken);

            Aplicar(entidad, validados);
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(entidad);
        }

        /// <summary>Baja lógica: el registro queda para las facturas que ya lo usan.</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
        {
            var entidad = await _context.Proveedores.FirstOrDefaultAsync(x => x.Id == id && x.Activo, cancellationToken);
            if (entidad == null) return NotFound();

            entidad.Activo = false;
            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        private static void Aplicar(Proveedor entidad, DatosComercialesValidados datos)
        {
            entidad.RazonSocial = datos.RazonSocial;
            entidad.Direccion = datos.Direccion;
            entidad.Localidad = datos.Localidad;
            entidad.Cuit = datos.Cuit;
            entidad.CondicionIva = datos.CondicionIva;
        }

        private async Task ValidarCuitNoRepetidoAsync(string cuit, long? idExcluido, CancellationToken cancellationToken)
        {
            // Se compara sin guiones: hay registros viejos guardados como "20111111112".
            var normalizado = EstudioContable.Domain.Cuit.Normalizar(cuit);
            var existentes = await _context.Proveedores
                .Where(x => x.Activo && x.Id != idExcluido)
                .Select(x => new { x.Cuit, x.RazonSocial })
                .ToListAsync(cancellationToken);

            var repetido = existentes.FirstOrDefault(x => EstudioContable.Domain.Cuit.Normalizar(x.Cuit) == normalizado);
            if (repetido != null)
                throw new InvalidOperationException($"Ya existe un proveedor con el CUIT {cuit}: {repetido.RazonSocial}.");
        }
    }
}
