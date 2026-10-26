using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Domain.Entities;

namespace EstudioContable.Domain.Interfaces
{
    public interface ICuentaRepository
    {
        Task<CuentaContable?> ObtenerPorIdAsync(long cuentaId, CancellationToken cancellationToken);
        Task<System.Collections.Generic.List<CuentaContable>> ObtenerTodasAsync(long clienteId, CancellationToken cancellationToken);
        void Agregar(CuentaContable cuenta);
    }
}
