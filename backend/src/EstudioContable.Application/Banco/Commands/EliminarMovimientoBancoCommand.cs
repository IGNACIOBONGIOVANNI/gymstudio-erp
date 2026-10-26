using MediatR;

namespace EstudioContable.Application.Banco.Commands
{
    public record EliminarMovimientoBancoCommand(long Id) : IRequest<Unit>;
}
