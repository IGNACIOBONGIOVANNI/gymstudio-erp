using System.Collections.Generic;
using EstudioContable.Application.Asientos.Queries.DTOs;
using MediatR;

namespace EstudioContable.Application.Asientos.Queries.ObtenerAsientos
{
    public record ObtenerAsientosQuery(long? ClienteId) : IRequest<List<AsientoDto>>;
}
