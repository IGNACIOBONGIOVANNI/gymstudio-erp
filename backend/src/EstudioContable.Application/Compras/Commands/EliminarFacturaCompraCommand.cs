using MediatR;

namespace EstudioContable.Application.Compras.Commands
{
    public record EliminarFacturaCompraCommand(long Id) : IRequest<Unit>;
}
