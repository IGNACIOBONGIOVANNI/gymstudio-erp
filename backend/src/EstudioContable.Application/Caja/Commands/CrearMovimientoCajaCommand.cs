using System;
using EstudioContable.Domain.Enums;
using MediatR;

namespace EstudioContable.Application.Caja.Commands
{
    public record CrearMovimientoCajaCommand(
        string ClienteId,
        DateTime Fecha,
        string Detalle,
        TipoMovimientoCaja TipoMovimiento,
        decimal Importe
    ) : IRequest<long>;
}
