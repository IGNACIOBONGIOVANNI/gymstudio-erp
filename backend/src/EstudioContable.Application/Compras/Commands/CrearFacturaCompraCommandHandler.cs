using EstudioContable.Application.Common.Exceptions;
using System;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Application.Notifications;
using EstudioContable.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Compras.Commands
{
    public class CrearFacturaCompraCommandHandler : IRequestHandler<CrearFacturaCompraCommand, long>
    {
        private readonly IAppDbContext _context;
        private readonly IMediator _mediator;

        public CrearFacturaCompraCommandHandler(IAppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<long> Handle(CrearFacturaCompraCommand request, CancellationToken cancellationToken)
        {
            var factura = new FacturaCompra(
                request.ClienteId,
                request.Fecha,
                request.TipoFactura,
                request.NumeroFactura,
                request.ProveedorNombre,
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
                        throw new NotFoundException("Producto", item.ProductoId);

                    factura.AgregarDetalle(producto, item.Cantidad);
                }

                _context.FacturasCompra.Add(factura);

            // Generar Asiento Contable
            long cuentaIdHaber = request.CondicionPago switch
            {
                Domain.Enums.CondicionPago.ContadoCaja => 101L, // Caja
                Domain.Enums.CondicionPago.TransferenciaBanco => 102L, // Banco
                _ => 202L // Proveedores
            };

            var maxNumero = await _context.AsientosContables
                .MaxAsync(a => (long?)a.NumeroAsiento, cancellationToken) ?? 0;
            var numeroAsiento = maxNumero + 1;

            var asiento = new AsientoContable(
                request.ClienteId,
                numeroAsiento,
                DateOnly.FromDateTime(request.Fecha),
                $"Factura de Compra {request.TipoFactura} {request.NumeroFactura}",
                1L // UsuarioCreadorId por defecto
            );

            asiento.AgregarLinea(501L, Math.Round(factura.Neto, 2), 0, "5.1.1.01 - Compras");
            if (factura.Iva > 0)
            {
                asiento.AgregarLinea(105L, Math.Round(factura.Iva, 2), 0, "1.1.2.01 - IVA Crédito Fiscal");
            }
            asiento.AgregarLinea(cuentaIdHaber, 0, Math.Round(factura.Total, 2), "Total Factura");

            asiento.Confirmar(1L); // UsuarioConfirmacionId por defecto
            _context.AsientosContables.Add(asiento);
            await _context.SaveChangesAsync(cancellationToken);

            factura.AsignarAsiento(asiento.Id);
            await _context.SaveChangesAsync(cancellationToken);

            // Publicar el evento de dominio
            await _mediator.Publish(new FacturaCompraCreadaNotification(factura), cancellationToken);

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
