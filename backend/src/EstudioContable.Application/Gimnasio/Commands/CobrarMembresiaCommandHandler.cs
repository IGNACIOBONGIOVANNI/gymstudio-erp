using EstudioContable.Application.Common.Exceptions;
using System;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Enums;
using EstudioContable.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Gimnasio.Commands
{
    public class CobrarMembresiaCommandHandler : IRequestHandler<CobrarMembresiaCommand, bool>
    {
        private readonly IAppDbContext _context;
        private readonly IFacturacionService _facturacionService;

        public CobrarMembresiaCommandHandler(IAppDbContext context, IFacturacionService facturacionService)
        {
            _context = context;
            _facturacionService = facturacionService;
        }

        public async Task<bool> Handle(CobrarMembresiaCommand request, CancellationToken cancellationToken)
        {
            // Iniciamos la transacción para garantizar Atomicidad (Gimnasio + Contabilidad)
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var membresia = await _context.Membresias
                    .Include(m => m.Plan)
                    .Include(m => m.Socio)
                    .FirstOrDefaultAsync(m => m.Id == request.MembresiaId, cancellationToken);

                if (membresia == null)
                    throw new NotFoundException("Membresía", request.MembresiaId);

                if (membresia.EstaPagada)
                    throw new InvalidOperationException("La membresía ya se encuentra pagada.");

                // 1. Logica de Gimnasio
                membresia.MarcarComoPagada();
                membresia.Socio.RenovarPlan(membresia.Plan.Id, membresia.Plan.DuracionMeses);

                var descripcionItem = $"Cuota Membresía - {membresia.Plan.Nombre}";
                var condicionPagoString = MetodoPago.ACondicionDePago(request.MetodoPago);

                // 2. Lógica contable: factura genérica, igual que el módulo de facturación
                var factura = new Factura
                {
                    Fecha = DateTime.UtcNow,
                    TipoOperacion = "Venta",
                    CondicionPago = condicionPagoString,
                    TipoComprobante = "B", // Comprobante a Consumidor Final
                    Cliente = $"{membresia.Socio.Apellido} {membresia.Socio.Nombre}",
                    SocioId = membresia.Socio.Id,
                    Subtotal = membresia.Plan.PrecioMensual,
                    Iva = 0,
                    Total = membresia.Plan.PrecioMensual,
                    Items = new System.Collections.Generic.List<FacturaItem>
                    {
                        new FacturaItem
                        {
                            Descripcion = descripcionItem,
                            Cantidad = 1,
                            PrecioUnitario = membresia.Plan.PrecioMensual,
                            Subtotal = membresia.Plan.PrecioMensual
                        }
                    }
                };

                // 3. Asiento, movimiento de caja/banco y vínculos: los genera el servicio
                // de facturación (participa de esta misma transacción).
                await _facturacionService.CrearFacturaAsync(factura, cancellationToken);

                // Hacemos el Commit de la transacción
                await transaction.CommitAsync(cancellationToken);

                return true;
            }
            catch
            {
                // Si falla cualquier paso (ej. el asiento queda desbalanceado), se revierte TODO (la cuota vuelve a estado impago)
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}

