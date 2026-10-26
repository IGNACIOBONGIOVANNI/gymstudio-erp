using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Interfaces;
using EstudioContable.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Infrastructure.Persistence.Repositories
{
    public class AsientoRepository : IAsientoRepository
    {
        private readonly AppDbContext _context;

        public AsientoRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AsientoContable?> ObtenerPorIdConLineasAsync(long asientoId, CancellationToken cancellationToken)
        {
            // Include obligatorio: sin las líneas cargadas, TotalDebe/TotalHaber
            // se calcularían sobre una colección vacía dentro del dominio.
            return await _context.AsientosContables
                .Include(a => a.Lineas)
                .FirstOrDefaultAsync(a => a.Id == asientoId, cancellationToken);
        }

        public async Task<System.Collections.Generic.List<AsientoContable>> ObtenerTodosAsync(long? clienteId, CancellationToken cancellationToken)
        {
            var query = _context.AsientosContables.Include(a => a.Lineas).AsQueryable();

            if (clienteId.HasValue && clienteId.Value > 0)
            {
                query = query.Where(a => a.ClienteId == clienteId.Value);
            }

            return await query.ToListAsync(cancellationToken);
        }

        public void Agregar(AsientoContable asiento)
        {
            _context.AsientosContables.Add(asiento);
        }

        public void Eliminar(AsientoContable asiento)
        {
            _context.AsientosContables.Remove(asiento);
        }

        public async Task<long> ObtenerMaximoNumeroAsientoAsync(long clienteId, CancellationToken cancellationToken)
        {
            var max = await _context.AsientosContables
                .Where(a => a.ClienteId == clienteId)
                .MaxAsync(a => (long?)a.NumeroAsiento, cancellationToken);
            return max ?? 0;
        }
    }
}
