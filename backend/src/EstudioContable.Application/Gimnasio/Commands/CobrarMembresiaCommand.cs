using System;
using EstudioContable.Domain.Enums;
using MediatR;

namespace EstudioContable.Application.Gimnasio.Commands
{
    public record CobrarMembresiaCommand(
        long MembresiaId,
        string MetodoPago
    ) : IRequest<bool>;
}
