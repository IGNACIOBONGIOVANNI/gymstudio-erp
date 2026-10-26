using EstudioContable.Application.Banco.Queries.DTOs;
using MediatR;

namespace EstudioContable.Application.Banco.Queries.ObtenerLibroBanco
{
    public record ObtenerLibroBancoQuery(string ClienteId) : IRequest<ResumenBancoDto>;
}
