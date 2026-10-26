using EstudioContable.Application.Caja.Queries.DTOs;
using MediatR;

namespace EstudioContable.Application.Caja.Queries.ObtenerLibroCaja
{
    public record ObtenerLibroCajaQuery(string ClienteId) : IRequest<ResumenCajaDto>;
}
