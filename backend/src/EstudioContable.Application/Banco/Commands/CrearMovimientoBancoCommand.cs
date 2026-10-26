using System;
using EstudioContable.Domain.Enums;
using MediatR;

namespace EstudioContable.Application.Banco.Commands
{
    public record CrearMovimientoBancoCommand(
        string ClienteId,
        DateTime Fecha,
        string Detalle,
        TipoMovimientoBanco TipoMovimiento,
        decimal Importe
    ) : IRequest<long>;
}
