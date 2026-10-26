using System;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Services;
using EstudioContable.Domain.Enums;
using MediatR;

namespace EstudioContable.Application.Banco.Commands
{
    public class CrearMovimientoBancoCommandHandler : IRequestHandler<CrearMovimientoBancoCommand, long>
    {
        private readonly ITesoreriaService _tesoreria;

        public CrearMovimientoBancoCommandHandler(ITesoreriaService tesoreria)
        {
            _tesoreria = tesoreria;
        }

        public Task<long> Handle(CrearMovimientoBancoCommand request, CancellationToken cancellationToken)
        {
            if (!Enum.IsDefined(request.TipoMovimiento))
                throw new ArgumentException("El tipo de movimiento debe ser Ingreso o Egreso.");

            var datos = new DatosMovimientoTesoreria(
                request.Fecha,
                request.Detalle,
                request.TipoMovimiento == TipoMovimientoBanco.Ingreso,
                request.Importe,
                request.CuentaContrapartidaId);

            return _tesoreria.CrearAsync(LibroTesoreria.Banco, datos, cancellationToken);
        }
    }
}
