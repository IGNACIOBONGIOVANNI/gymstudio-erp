using System;
using System.Collections.Generic;
using EstudioContable.Domain.Enums;

namespace EstudioContable.Application.Asientos.Queries.DTOs
{
    public record AsientoDto(
        long Id,
        long ClienteId,
        long NumeroAsiento,
        DateOnly Fecha,
        string Descripcion,
        EstadoAsiento Estado,
        long UsuarioCreadorId,
        DateTimeOffset FechaCreacion,
        DateTimeOffset? FechaConfirmacion,
        long? UsuarioConfirmacionId,
        decimal TotalDebe,
        decimal TotalHaber,
        bool EstaBalanceado,
        List<LineaAsientoDto> Lineas
    );

    public record LineaAsientoDto(
        long Id,
        long CuentaId,
        string NombreCuenta,
        decimal Debe,
        decimal Haber,
        string? Descripcion,
        short Orden
    );
}
