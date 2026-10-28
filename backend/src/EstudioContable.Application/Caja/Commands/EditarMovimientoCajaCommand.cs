using System;
using MediatR;

namespace EstudioContable.Application.Caja.Commands
{
    public record EditarMovimientoCajaCommand(
        long Id,
        DateTime Fecha,
        string Detalle,
        decimal? Ingreso,
        decimal? Egreso,
        long? CuentaContrapartidaId = null,
        long? ClienteComercialId = null,
        long? ProveedorId = null
    ) : IRequest<Unit>;
}
