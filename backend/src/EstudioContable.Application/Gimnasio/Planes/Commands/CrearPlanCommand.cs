using System;
using MediatR;

namespace EstudioContable.Application.Gimnasio.Planes.Commands
{
    public record CrearPlanCommand(
        string Nombre,
        decimal PrecioMensual,
        int DuracionMeses
    ) : IRequest<long>;
}
