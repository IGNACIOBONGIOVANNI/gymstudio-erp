using EstudioContable.Application.Asientos.Queries.DTOs;
using MediatR;

namespace EstudioContable.Application.Asientos.Queries.ObtenerAsientoPorId
{
    public record ObtenerAsientoPorIdQuery(long AsientoId) : IRequest<AsientoDto?>;
}
