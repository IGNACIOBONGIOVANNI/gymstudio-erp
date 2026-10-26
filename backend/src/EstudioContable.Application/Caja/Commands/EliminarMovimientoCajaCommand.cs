using MediatR;

namespace EstudioContable.Application.Caja.Commands
{
    public record EliminarMovimientoCajaCommand(long Id) : IRequest<Unit>;
}
