using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using MediatR;

namespace EstudioContable.Application.Banco.Commands
{
    public class CrearMovimientoBancoCommandHandler : IRequestHandler<CrearMovimientoBancoCommand, long>
    {
        private readonly IAppDbContext _context;

        public CrearMovimientoBancoCommandHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<long> Handle(CrearMovimientoBancoCommand request, CancellationToken cancellationToken)
        {
            var movimiento = new MovimientoBanco(
                request.ClienteId,
                request.Fecha,
                request.Detalle,
                request.TipoMovimiento,
                request.Importe
            );

            _context.MovimientosBanco.Add(movimiento);
            await _context.SaveChangesAsync(cancellationToken);

            return movimiento.Id;
        }
    }
}
