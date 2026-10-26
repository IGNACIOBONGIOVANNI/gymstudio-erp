using EstudioContable.Domain.Entities;

namespace EstudioContable.Application.Cuentas.Commands.CrearCuenta
{
    public record CrearCuentaResult(
        long CuentaId,
        string Codigo,
        string Nombre,
        NaturalezaCuenta Naturaleza
    );
}
