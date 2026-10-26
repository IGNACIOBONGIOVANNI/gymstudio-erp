using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using MediatR;

namespace EstudioContable.Application.Caja.Commands
{
    public class CrearMovimientoCajaCommandHandler : IRequestHandler<CrearMovimientoCajaCommand, long>
    {
        private readonly IAppDbContext _context;

        public CrearMovimientoCajaCommandHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<long> Handle(CrearMovimientoCajaCommand request, CancellationToken cancellationToken)
        {
            var movimiento = new MovimientoCaja(
                request.ClienteId,
                request.Fecha,
                request.Detalle,
                request.TipoMovimiento,
                request.Importe
            );

            _context.MovimientosCaja.Add(movimiento);
            await _context.SaveChangesAsync(cancellationToken);

            return movimiento.Id;
        }
    }
}
