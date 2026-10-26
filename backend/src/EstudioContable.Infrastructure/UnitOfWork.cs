using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Common.Interfaces;
using EstudioContable.Infrastructure.Persistence;

namespace EstudioContable.Infrastructure.Persistence
{
    /// <summary>
    /// Envuelve AppDbContext.SaveChangesAsync. Al usar un único DbContext por
    /// request (scoped), un solo SaveChangesAsync ya persiste todos los cambios
    /// trackeados (agregado AsientoContable + sus líneas) dentro de una única
    /// transacción implícita de EF Core.
    /// </summary>
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
        }

        public Task<int> GuardarCambiosAsync(CancellationToken cancellationToken)
            => _context.SaveChangesAsync(cancellationToken);
    }
}
