using System;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Application.Notifications;
using EstudioContable.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Ventas.Commands
{
    public class CrearFacturaVentaCommandHandler : IRequestHandler<CrearFacturaVentaCommand, long>
    {
        private readonly IAppDbContext _context;
        private readonly IMediator _mediator;

        public CrearFacturaVentaCommandHandler(IAppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<long> Handle(CrearFacturaVentaCommand request, CancellationToken cancellationToken)
        {
            var factura = new FacturaVenta(
                request.ClienteId,
                request.Fecha,
                request.TipoFactura,
                request.NumeroFactura,
                request.ClienteNombre,
                request.Cuit,
                request.CondicionPago
            );

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                foreach (var item in request.Items)
                {
                    var producto = await _context.Productos.FindAsync(new object[] { item.ProductoId }, cancellationToken);
                    if (producto == null)
                        throw new Exception($"El producto con ID {item.ProductoId} no existe.");

                    factura.AgregarDetalle(producto, item.Cantidad);
                }

                _context.FacturasVenta.Add(factura);

            // Generar Asiento Contable
            long cuentaIdDebe = request.CondicionPago switch
            {
                Domain.Enums.CondicionPago.ContadoCaja => 101L, // Caja
                Domain.Enums.CondicionPago.TransferenciaBanco => 102L, // Banco
                _ => 103L // Deudores por Ventas
            };

            var maxNumero = await _context.AsientosContables
                .MaxAsync(a => (long?)a.NumeroAsiento, cancellationToken) ?? 0;
            var numeroAsiento = maxNumero + 1;

            var asiento = new AsientoContable(
                request.ClienteId,
                numeroAsiento,
                DateOnly.FromDateTime(request.Fecha),
                $"Factura de Venta {request.TipoFactura} {request.NumeroFactura}",
                1L // UsuarioCreadorId por defecto
            );

            asiento.AgregarLinea(cuentaIdDebe, Math.Round(factura.Total, 2), 0, "Total Factura");
            asiento.AgregarLinea(104L, 0, Math.Round(factura.Neto, 2), "Ventas");
            if (factura.Iva > 0)
            {
                asiento.AgregarLinea(201L, 0, Math.Round(factura.Iva, 2), "2.1.1.01 - IVA Débito Fiscal");
            }

            asiento.Confirmar(1L); // UsuarioConfirmacionId por defecto
            _context.AsientosContables.Add(asiento);
            await _context.SaveChangesAsync(cancellationToken);

            factura.AsignarAsiento(asiento.Id);
            await _context.SaveChangesAsync(cancellationToken);

            // Publicar el evento de dominio
            await _mediator.Publish(new FacturaVentaCreadaNotification(factura), cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return factura.Id;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}
