using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Compras.Commands
{
    public class EliminarFacturaCompraCommandHandler : IRequestHandler<EliminarFacturaCompraCommand, Unit>
    {
        private readonly IAppDbContext _context;

        public EliminarFacturaCompraCommandHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<Unit> Handle(EliminarFacturaCompraCommand request, CancellationToken cancellationToken)
        {
            var factura = await _context.FacturasCompra.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

            if (factura == null)
                throw new Exception("La factura de compra no existe");

            var movimientosCaja = await _context.MovimientosCaja
                .Where(m => m.FacturaId == request.Id)
                .ToListAsync(cancellationToken);

            if (movimientosCaja.Any())
            {
                _context.MovimientosCaja.RemoveRange(movimientosCaja);
                // 🔥 MAGIA: Si querés, acá podés invalidar o ajustar algo más, pero con el RemoveRange está perfecto.
            }

            var movimientosBanco = await _context.MovimientosBanco
                .Where(m => m.FacturaId == request.Id)
                .ToListAsync(cancellationToken);

            if (movimientosBanco.Any())
            {
                _context.MovimientosBanco.RemoveRange(movimientosBanco);
            }

            var asiento = await _context.AsientosContables
                .FirstOrDefaultAsync(a => a.Id == factura.AsientoContableId || 
                                         a.Descripcion.Contains($"Factura de Compra N° {factura.Id}"), cancellationToken);
            
            if (asiento != null)
            {
                _context.AsientosContables.Remove(asiento);
            }

            _context.FacturasCompra.Remove(factura);
            await _context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}