using System;
using EstudioContable.Domain.Enums;
using MediatR;

namespace EstudioContable.Application.Gimnasio.Commands
{
    public record AsignarYCobrarMembresiaCommand(
        long SocioId,
        long PlanId,
        string MetodoPago
    ) : IRequest<long>;
}
