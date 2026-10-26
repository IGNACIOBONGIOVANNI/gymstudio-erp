using System.Collections.Generic;
using EstudioContable.Application.Gimnasio.Socios.Dtos;
using MediatR;

namespace EstudioContable.Application.Gimnasio.Socios.Queries
{
    public record ObtenerEstadoSociosQuery : IRequest<List<SocioEstadoDto>>;
}
