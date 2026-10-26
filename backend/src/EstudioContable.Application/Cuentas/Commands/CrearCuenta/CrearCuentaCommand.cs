using MediatR;
using EstudioContable.Domain.Entities;

namespace EstudioContable.Application.Cuentas.Commands.CrearCuenta
{
    public record CrearCuentaCommand(
        long ClienteId,
        string Codigo,
        string Nombre,
        NaturalezaCuenta Naturaleza,
        long? CuentaPadreId
    ) : IRequest<CrearCuentaResult>;
}
