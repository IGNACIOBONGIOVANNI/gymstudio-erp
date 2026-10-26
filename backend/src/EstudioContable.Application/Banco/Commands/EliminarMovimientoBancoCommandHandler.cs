using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Services;
using MediatR;

namespace EstudioContable.Application.Banco.Commands
{
    /// <summary>
    /// Elimina un movimiento cargado a mano: registra el contraasiento de su asiento y lo
    /// quita del libro. Los movimientos generados por una factura no se eliminan desde acá.
    /// </summary>
    public class EliminarMovimientoBancoCommandHandler : IRequestHandler<EliminarMovimientoBancoCommand, Unit>
    {
        private readonly ITesoreriaService _tesoreria;

        public EliminarMovimientoBancoCommandHandler(ITesoreriaService tesoreria)
        {
            _tesoreria = tesoreria;
        }

        public async Task<Unit> Handle(EliminarMovimientoBancoCommand request, CancellationToken cancellationToken)
        {
            await _tesoreria.EliminarAsync(LibroTesoreria.Banco, request.Id, cancellationToken);
            return Unit.Value;
        }
    }
}
