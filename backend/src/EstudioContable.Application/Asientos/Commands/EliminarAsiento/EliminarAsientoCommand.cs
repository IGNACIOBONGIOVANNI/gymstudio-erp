using MediatR;

namespace EstudioContable.Application.Asientos.Commands.EliminarAsiento
{
    public record EliminarAsientoCommand(long Id) : IRequest;
}
