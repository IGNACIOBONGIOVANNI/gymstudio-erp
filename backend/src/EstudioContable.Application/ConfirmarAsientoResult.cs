using System;
using EstudioContable.Domain.Enums;

namespace EstudioContable.Application.Asientos.Commands.ConfirmarAsiento
{
    /// <summary>
    /// Proyección de sólo los datos relevantes para el cliente tras confirmar
    /// (nunca se devuelve la entidad de dominio directamente hacia afuera de Application).
    /// </summary>
    public record ConfirmarAsientoResult(
        long AsientoId,
        long NumeroAsiento,
        EstadoAsiento Estado,
        DateTimeOffset FechaConfirmacion,
        long UsuarioConfirmacionId,
        decimal TotalDebe,
        decimal TotalHaber
    );
}
