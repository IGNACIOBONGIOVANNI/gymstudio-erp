using System;
using MediatR;

namespace EstudioContable.Application.Banco.Commands
{
    public record EditarMovimientoBancoCommand(
        long Id,
        DateTime Fecha,
        string Detalle,
        decimal? Ingreso,
        decimal? Egreso
    ) : IRequest<Unit>;
}
