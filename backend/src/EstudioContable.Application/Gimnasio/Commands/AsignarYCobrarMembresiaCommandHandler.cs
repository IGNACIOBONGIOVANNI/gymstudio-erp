using EstudioContable.Application.Common.Exceptions;
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
            MetodoPago.ACondicionDePago(request.MetodoPago); // valida antes de tocar nada

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var socio = await _context.Socios.FirstOrDefaultAsync(s => s.Id == request.SocioId, cancellationToken);
                if (socio == null) throw new NotFoundException("Socio", request.SocioId);

                var plan = await _context.Planes.FirstOrDefaultAsync(p => p.Id == request.PlanId, cancellationToken);
                if (plan == null) throw new NotFoundException("Plan", request.PlanId);

                // El período se calcula una sola vez: antes se tomaba la hora actual dos
                // veces y el vencimiento podía quedar microsegundos antes del inicio.
                var periodo = socio.RenovarPlan(plan.Id, plan.DuracionMeses);

                // 1. Crear Membresía
                var membresia = new Membresia(
                    socioId: socio.Id,
                    planId: plan.Id,
                    fechaInicio: periodo.Inicio,
                    fechaVencimiento: periodo.Vencimiento,
                    estaPagada: true
                );
                _context.Membresias.Add(membresia);
                await _context.SaveChangesAsync(cancellationToken); // Genera membresia.Id

                // 2. Facturar Cuota a través del Servicio (Esto delega Diario y Caja a Contabilidad)
                var condicionPagoString = MetodoPago.ACondicionDePago(request.MetodoPago);

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
                            Descripcion = $"Cuota Membresía - {plan.Nombre}",
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

