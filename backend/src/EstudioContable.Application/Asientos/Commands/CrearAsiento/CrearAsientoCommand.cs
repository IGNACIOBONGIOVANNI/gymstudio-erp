using MediatR;
using System;
using System.Collections.Generic;

namespace EstudioContable.Application.Asientos.Commands.CrearAsiento
{
    public record CrearAsientoCommand(
        long ClienteId,
        long NumeroAsiento,
        DateOnly Fecha,
        string Descripcion,
        long UsuarioCreadorId,
        List<LineaAsientoDto> Lineas
    ) : IRequest<CrearAsientoResult>;

    public record LineaAsientoDto(
        long CuentaId,
        decimal Debe,
        decimal Haber,
        string? Descripcion
    );
}
