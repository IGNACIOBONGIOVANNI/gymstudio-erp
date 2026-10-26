using System;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Enums;
using MediatR;

namespace EstudioContable.Application.Notifications.Handlers
{
    public class MembresiaCobradaNotificationHandler : INotificationHandler<MembresiaCobradaNotification>
    {
        private readonly IAppDbContext _context;

        public MembresiaCobradaNotificationHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task Handle(MembresiaCobradaNotification notification, CancellationToken cancellationToken)
        {
            // Solo procesamos pagos al contado (Caja) o transferencia (Banco).
            if (notification.CondicionPago == CondicionPago.CuentaCorriente)
                return;

            string clienteId = "1"; // Default para Gym por ahora

            if (notification.CondicionPago == CondicionPago.ContadoCaja)
            {
                var movimientoCaja = new MovimientoCaja(
                    clienteId: clienteId,
                    fecha: DateTime.UtcNow,
                    detalle: notification.Descripcion,
                    tipoMovimiento: TipoMovimientoCaja.Ingreso,
                    importe: notification.Monto
                );
                
                _context.MovimientosCaja.Add(movimientoCaja);
            }
            else if (notification.CondicionPago == CondicionPago.TransferenciaBanco)
            {
                var movimientoBanco = new MovimientoBanco(
                    clienteId: clienteId,
                    fecha: DateTime.UtcNow,
                    detalle: notification.Descripcion,
                    tipoMovimiento: TipoMovimientoBanco.Ingreso,
                    importe: notification.Monto
                );

                _context.MovimientosBanco.Add(movimientoBanco);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
