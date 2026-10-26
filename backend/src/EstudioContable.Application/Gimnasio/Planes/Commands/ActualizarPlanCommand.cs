using MediatR;

namespace EstudioContable.Application.Gimnasio.Planes.Commands
{
    public record ActualizarPlanCommand(
        long Id,
        string Nombre,
        decimal PrecioMensual,
        int DuracionMeses
    ) : IRequest<bool>;
}
