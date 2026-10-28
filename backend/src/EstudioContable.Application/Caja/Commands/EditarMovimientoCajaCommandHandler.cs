using System;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Services;
using MediatR;

namespace EstudioContable.Application.Caja.Commands
{
    public class EditarMovimientoCajaCommandHandler : IRequestHandler<EditarMovimientoCajaCommand, Unit>
    {
        private readonly ITesoreriaService _tesoreria;

        public EditarMovimientoCajaCommandHandler(ITesoreriaService tesoreria)
        {
            _tesoreria = tesoreria;
        }

        public async Task<Unit> Handle(EditarMovimientoCajaCommand request, CancellationToken cancellationToken)
        {
            var esIngreso = request.Ingreso is > 0;
            var esEgreso = request.Egreso is > 0;
            if (esIngreso == esEgreso)
                throw new ArgumentException("Indicá un importe mayor a cero como ingreso o como egreso (no los dos).");

            var datos = new DatosMovimientoTesoreria(
                request.Fecha,
                request.Detalle,
                esIngreso,
                esIngreso ? request.Ingreso!.Value : request.Egreso!.Value,
                request.CuentaContrapartidaId,
                request.ClienteComercialId,
                request.ProveedorId);

            await _tesoreria.EditarAsync(LibroTesoreria.Caja, request.Id, datos, cancellationToken);
            return Unit.Value;
        }
    }
}
