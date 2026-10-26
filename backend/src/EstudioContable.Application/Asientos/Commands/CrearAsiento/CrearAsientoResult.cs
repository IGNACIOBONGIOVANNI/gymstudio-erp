using System;
using EstudioContable.Domain.Enums;

namespace EstudioContable.Application.Asientos.Commands.CrearAsiento
{
    public record CrearAsientoResult(
        long AsientoId,
        long NumeroAsiento,
        EstadoAsiento Estado,
        decimal TotalDebe,
        decimal TotalHaber,
        DateTimeOffset FechaCreacion
    );
}
