using System;
using EstudioContable.Domain.Enums;
using MediatR;

namespace EstudioContable.Application.Notifications
{
    public record MembresiaCobradaNotification(
        decimal Monto,
        string Descripcion,
        CondicionPago CondicionPago
    ) : INotification;
}
