using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Domain.Entities;

namespace EstudioContable.Application.Interfaces
{
    public interface IFacturacionService
    {
        Task<Factura> CrearFacturaAsync(Factura factura, CancellationToken cancellationToken);
    }
}
