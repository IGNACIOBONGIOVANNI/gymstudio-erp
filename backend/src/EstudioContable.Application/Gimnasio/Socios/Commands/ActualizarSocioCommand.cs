using MediatR;

namespace EstudioContable.Application.Gimnasio.Socios.Commands
{
    public record ActualizarSocioCommand(
        long Id,
        string Nombre,
        string Apellido,
        string Dni,
        string? Telefono
    ) : IRequest<bool>;
}
