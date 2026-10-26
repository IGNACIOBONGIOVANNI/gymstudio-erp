using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Services;
using MediatR;

namespace EstudioContable.Application.Caja.Commands
{
    /// <summary>
    /// Elimina un movimiento cargado a mano: registra el contraasiento de su asiento y lo
    /// quita del libro. Los movimientos generados por una factura no se eliminan desde acá.
    /// </summary>
    public class EliminarMovimientoCajaCommandHandler : IRequestHandler<EliminarMovimientoCajaCommand, Unit>
    {
        private readonly ITesoreriaService _tesoreria;

        public EliminarMovimientoCajaCommandHandler(ITesoreriaService tesoreria)
        {
            _tesoreria = tesoreria;
        }

        public async Task<Unit> Handle(EliminarMovimientoCajaCommand request, CancellationToken cancellationToken)
        {
            await _tesoreria.EliminarAsync(LibroTesoreria.Caja, request.Id, cancellationToken);
            return Unit.Value;
        }
    }
}
