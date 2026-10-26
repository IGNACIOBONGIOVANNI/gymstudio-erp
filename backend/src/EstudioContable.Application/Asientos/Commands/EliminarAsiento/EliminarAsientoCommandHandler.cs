using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Common.Interfaces;
using EstudioContable.Domain.Interfaces;
using MediatR;

namespace EstudioContable.Application.Asientos.Commands.EliminarAsiento
{
    public class EliminarAsientoCommandHandler : IRequestHandler<EliminarAsientoCommand>
    {
        private readonly IAsientoRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly EstudioContable.Application.Interfaces.IAppDbContext _context;

        public EliminarAsientoCommandHandler(IAsientoRepository repository, IUnitOfWork unitOfWork, EstudioContable.Application.Interfaces.IAppDbContext context)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _context = context;
        }

        public async Task Handle(EliminarAsientoCommand request, CancellationToken cancellationToken)
        {
            var asiento = await _repository.ObtenerPorIdConLineasAsync(request.Id, cancellationToken);
            if (asiento == null) return;

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var factura = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                    _context.Facturas,
                    f => f.AsientoContableId == request.Id || 
                         asiento.Descripcion.Contains("Factura de Venta N° " + f.Id) ||
                         asiento.Descripcion.Contains("Factura de Compra N° " + f.Id), 
                    cancellationToken);

                if (factura != null)
                {
                    var movCaja = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                        System.Linq.Queryable.Where(_context.MovimientosCaja, m => m.FacturaId == factura.Id), cancellationToken);
                    if (movCaja.Count > 0) _context.MovimientosCaja.RemoveRange(movCaja);

                    var movBanco = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                        System.Linq.Queryable.Where(_context.MovimientosBanco, m => m.FacturaId == factura.Id), cancellationToken);
                    if (movBanco.Count > 0) _context.MovimientosBanco.RemoveRange(movBanco);

                    _context.Facturas.Remove(factura);
                }

                _repository.Eliminar(asiento);
                await _context.SaveChangesAsync(cancellationToken);
                
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}
