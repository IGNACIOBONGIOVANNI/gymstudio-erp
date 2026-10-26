using System.Collections.Generic;
using EstudioContable.Application.Asientos.Queries.DTOs;
using MediatR;

namespace EstudioContable.Application.Asientos.Queries.ObtenerSaldosMayor
{
    public record ObtenerSaldosMayorQuery(long ClienteId) : IRequest<List<SaldoMayorDto>>;
}
