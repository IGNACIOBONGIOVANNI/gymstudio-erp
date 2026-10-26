using System;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Caja.Commands
{
    public class EditarMovimientoCajaCommandHandler : IRequestHandler<EditarMovimientoCajaCommand, Unit>
    {
        private readonly IAppDbContext _context;

        public EditarMovimientoCajaCommandHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<Unit> Handle(EditarMovimientoCajaCommand request, CancellationToken cancellationToken)
        {
            var movimiento = await _context.MovimientosCaja.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            
            if (movimiento == null)
                throw new Exception("El movimiento de caja no existe");

            var tipoMovimiento = request.Ingreso.HasValue && request.Ingreso.Value > 0
                ? TipoMovimientoCaja.Ingreso
                : TipoMovimientoCaja.Egreso;

            var importe = request.Ingreso.HasValue && request.Ingreso.Value > 0
                ? request.Ingreso.Value
                : (request.Egreso ?? 0);

            movimiento.Actualizar(request.Fecha, request.Detalle, tipoMovimiento, importe);

            await _context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
