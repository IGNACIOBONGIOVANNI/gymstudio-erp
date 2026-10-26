using System;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Gimnasio.Commands
{
    public class AsignarYCobrarMembresiaCommandHandler : IRequestHandler<AsignarYCobrarMembresiaCommand, long>
    {
        private readonly IAppDbContext _context;
        private readonly IFacturacionService _facturacionService;

        public AsignarYCobrarMembresiaCommandHandler(IAppDbContext context, IFacturacionService facturacionService)
        {
            _context = context;
            _facturacionService = facturacionService;
        }

        public async Task<long> Handle(AsignarYCobrarMembresiaCommand request, CancellationToken cancellationToken)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var socio = await _context.Socios.FirstOrDefaultAsync(s => s.Id == request.SocioId, cancellationToken);
                if (socio == null) throw new Exception($"Socio {request.SocioId} no encontrado.");

                var plan = await _context.Planes.FirstOrDefaultAsync(p => p.Id == request.PlanId, cancellationToken);
                if (plan == null) throw new Exception($"Plan {request.PlanId} no encontrado.");

                socio.RenovarPlan(plan.Id, plan.DuracionMeses);

                // 1. Crear Membresa
                var membresia = new Membresia(
                    socioId: socio.Id,
                    planId: plan.Id,
                    fechaInicio: DateTime.UtcNow,
                    fechaVencimiento: socio.FechaVencimiento.Value,
                    estaPagada: true
                );
                _context.Membresias.Add(membresia);
                await _context.SaveChangesAsync(cancellationToken); // Genera membresia.Id

                // 2. Facturar Cuota a travs del Servicio (Esto delega Diario y Caja a Contabilidad)
                var condicionPagoString = (request.MetodoPago?.ToLower() == "banco") ? "TransferenciaBanco" : "ContadoCaja";
                
                var factura = new Factura
                {
                    Fecha = DateTime.UtcNow,
                    TipoOperacion = "Venta",
                    CondicionPago = condicionPagoString,
                    TipoComprobante = "B",
                    Cliente = $"{socio.Apellido} {socio.Nombre}",
                    SocioId = socio.Id,
                    Subtotal = plan.PrecioMensual,
                    Iva = 0,
                    Total = plan.PrecioMensual,
                    Items = new System.Collections.Generic.List<FacturaItem>
                    {
                        new FacturaItem
                        {
                            Descripcion = $"Cuota Membresa - {plan.Nombre}",
                            Cantidad = 1,
                            PrecioUnitario = plan.PrecioMensual,
                            Subtotal = plan.PrecioMensual
                        }
                    }
                };

                await _facturacionService.CrearFacturaAsync(factura, cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return membresia.Id;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}

