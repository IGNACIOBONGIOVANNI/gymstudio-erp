using System;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Banco.Commands
{
    public class EliminarMovimientoBancoCommandHandler : IRequestHandler<EliminarMovimientoBancoCommand, Unit>
    {
        private readonly IAppDbContext _context;

        public EliminarMovimientoBancoCommandHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<Unit> Handle(EliminarMovimientoBancoCommand request, CancellationToken cancellationToken)
        {
            var movimiento = await _context.MovimientosBanco.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            if (movimiento == null) throw new Exception("El movimiento de banco no existe");

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                if (movimiento.FacturaId.HasValue)
                {
                    var facturaAsociada = await _context.Facturas.FirstOrDefaultAsync(f => f.Id == movimiento.FacturaId.Value, cancellationToken);
                    if (facturaAsociada != null)
                    {
                        var asientoFactura = await _context.AsientosContables.FirstOrDefaultAsync(a => a.Id == facturaAsociada.AsientoContableId || 
                            a.Descripcion.Contains("Factura de Venta N° " + facturaAsociada.Id) || 
                            a.Descripcion.Contains("Factura de Compra N° " + facturaAsociada.Id), cancellationToken);
                        
                        if (asientoFactura != null) _context.AsientosContables.Remove(asientoFactura);
                        _context.Facturas.Remove(facturaAsociada);
                    }
                }
                
                var asiento = await _context.AsientosContables.FirstOrDefaultAsync(a => a.Descripcion.Contains(movimiento.Detalle), cancellationToken);
                if (asiento != null) _context.AsientosContables.Remove(asiento);

                _context.MovimientosBanco.Remove(movimiento);

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            return Unit.Value;
        }
    }
}
