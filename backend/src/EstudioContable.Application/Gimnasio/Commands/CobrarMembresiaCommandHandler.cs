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
        private readonly IMediator _mediator;

        public CobrarMembresiaCommandHandler(IAppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<bool> Handle(CobrarMembresiaCommand request, CancellationToken cancellationToken)
        {
            // Iniciamos la transaccin para garantizar Atomicidad (Gimnasio + Contabilidad)
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var membresia = await _context.Membresias
                    .Include(m => m.Plan)
                    .Include(m => m.Socio)
                    .FirstOrDefaultAsync(m => m.Id == request.MembresiaId, cancellationToken);

                if (membresia == null)
                    throw new Exception($"Membresa con ID {request.MembresiaId} no encontrada.");

                if (membresia.EstaPagada)
                    throw new Exception("La membresa ya se encuentra pagada.");

                // 1. Logica de Gimnasio
                membresia.MarcarComoPagada();
                membresia.Socio.RenovarPlan(membresia.Plan.Id, membresia.Plan.DuracionMeses);

                var descripcionItem = $"Cuota Membresa - {membresia.Plan.Nombre}";
                var condicionPagoString = (request.MetodoPago?.ToLower() == "banco") ? "TransferenciaBanco" : "ContadoCaja";

                // 2. Lgica Contable (Creacin de la Factura genrica tal como lo hace el Mculo Contable)
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
                
                _context.Facturas.Add(factura);

                // 3. Generacin de Asiento de Partida Doble vinculado a la factura
                var maxNumero = await _context.AsientosContables
                    .MaxAsync(a => (long?)a.NumeroAsiento, cancellationToken) ?? 0;
                
                var asiento = new AsientoContable(
                    clienteId: membresia.SocioId,
                    numeroAsiento: maxNumero + 1,
                    fecha: DateOnly.FromDateTime(DateTime.UtcNow),
                    descripcion: $"Factura de Venta B - {descripcionItem}",
                    usuarioCreadorId: 1 
                );

                var cuentaCaja = await _context.CuentasContables.FirstOrDefaultAsync(c => c.Nombre.Contains("Caja"), cancellationToken);
                var cuentaBanco = await _context.CuentasContables.FirstOrDefaultAsync(c => c.Nombre.Contains("Banco"), cancellationToken);
                var cuentaVentas = await _context.CuentasContables.FirstOrDefaultAsync(c => c.Nombre.Contains("Cuota") || c.Nombre.Contains("Ingreso") || c.Nombre.Contains("Venta"), cancellationToken);

                long cuentaDebeId = (request.MetodoPago?.ToLower() == "banco") ? (cuentaBanco?.Id ?? 2) : (cuentaCaja?.Id ?? 1);
                long cuentaHaberId = cuentaVentas?.Id ?? 4; 

                asiento.AgregarLinea(cuentaId: cuentaDebeId, debe: membresia.Plan.PrecioMensual, haber: 0, descripcion: descripcionItem);
                asiento.AgregarLinea(cuentaId: cuentaHaberId, debe: 0, haber: membresia.Plan.PrecioMensual, descripcion: descripcionItem);

                asiento.Confirmar(1);
                _context.AsientosContables.Add(asiento);

                // Guardamos para generar los IDs de Factura y Asiento
                await _context.SaveChangesAsync(cancellationToken);

                // 4. Vinculamos la Factura con el Asiento
                factura.AsientoContableId = asiento.Id;

                // 5. Generamos el Movimiento de Caja/Banco para el Libro Caja (tal como lo hace FacturasController)
                if (request.MetodoPago?.ToLower() == "banco")
                {
                    var mov = new MovimientoBanco("1", DateTime.UtcNow, $"Factura de Venta N {factura.Id} - {factura.Cliente}", TipoMovimientoBanco.Ingreso, factura.Total, factura.Id);
                    _context.MovimientosBanco.Add(mov);
                }
                else
                {
                    var mov = new MovimientoCaja("1", DateTime.UtcNow, $"Factura de Venta N {factura.Id} - {factura.Cliente}", TipoMovimientoCaja.Ingreso, factura.Total, factura.Id);
                    _context.MovimientosCaja.Add(mov);
                }

                await _context.SaveChangesAsync(cancellationToken);

                // Hacemos el Commit de la transaccin
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

