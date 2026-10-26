using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Infrastructure.Persistence.Repositories
{
    public class CuentaRepository : ICuentaRepository
    {
        private readonly AppDbContext _context;

        public CuentaRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CuentaContable?> ObtenerPorIdAsync(long cuentaId, CancellationToken cancellationToken)
        {
            return await _context.Set<CuentaContable>()
                .FirstOrDefaultAsync(c => c.Id == cuentaId, cancellationToken);
        }

        public void Agregar(CuentaContable cuenta)
        {
            _context.Set<CuentaContable>().Add(cuenta);
        }

        public async Task<System.Collections.Generic.List<CuentaContable>> ObtenerTodasAsync(long clienteId, CancellationToken cancellationToken)
        {
            return await _context.Set<CuentaContable>()
                .Where(c => c.ClienteId == clienteId)
                .ToListAsync(cancellationToken);
        }
    }
}
