using System;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Services;
using EstudioContable.Domain.Enums;
using MediatR;

namespace EstudioContable.Application.Caja.Commands
{
    public class CrearMovimientoCajaCommandHandler : IRequestHandler<CrearMovimientoCajaCommand, long>
    {
        private readonly ITesoreriaService _tesoreria;

        public CrearMovimientoCajaCommandHandler(ITesoreriaService tesoreria)
        {
            _tesoreria = tesoreria;
        }

        public Task<long> Handle(CrearMovimientoCajaCommand request, CancellationToken cancellationToken)
        {
            if (!Enum.IsDefined(request.TipoMovimiento))
                throw new ArgumentException("El tipo de movimiento debe ser Ingreso o Egreso.");

            var datos = new DatosMovimientoTesoreria(
                request.Fecha,
                request.Detalle,
                request.TipoMovimiento == TipoMovimientoCaja.Ingreso,
                request.Importe,
                request.CuentaContrapartidaId);

            return _tesoreria.CrearAsync(LibroTesoreria.Caja, datos, cancellationToken);
        }
    }
}
