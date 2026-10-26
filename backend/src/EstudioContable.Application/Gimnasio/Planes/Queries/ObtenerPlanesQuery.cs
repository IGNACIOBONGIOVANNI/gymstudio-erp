using System.Collections.Generic;
using EstudioContable.Application.Gimnasio.Planes.Dtos;
using MediatR;

namespace EstudioContable.Application.Gimnasio.Planes.Queries
{
    public record ObtenerPlanesQuery : IRequest<List<PlanDto>>;
}
