using System;
using MediatR;

namespace EstudioContable.Application.Gimnasio.Socios.Commands
{
    public record CrearSocioCommand(
        string Nombre,
        string Apellido,
        string Dni,
        string? Telefono
    ) : IRequest<long>;
}
