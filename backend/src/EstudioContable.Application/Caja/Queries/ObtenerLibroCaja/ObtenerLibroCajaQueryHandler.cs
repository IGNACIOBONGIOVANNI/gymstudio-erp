using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Caja.Queries.DTOs;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Caja.Queries.ObtenerLibroCaja
{
    public class ObtenerLibroCajaQueryHandler : IRequestHandler<ObtenerLibroCajaQuery, ResumenCajaDto>
    {
        private readonly IAppDbContext _context;

        public ObtenerLibroCajaQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<ResumenCajaDto> Handle(ObtenerLibroCajaQuery request, CancellationToken cancellationToken)
        {
            var movimientosDb = await _context.MovimientosCaja
                .Where(x => x.ClienteId == request.ClienteId)
                .OrderBy(x => x.Fecha)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

            // Asientos compartidos con el otro libro = transferencias entre Caja y Banco.
            var asientosDeTransferencias = (await _context.MovimientosBanco
                .Where(m => m.AsientoContableId != null)
                .Select(m => m.AsientoContableId!.Value)
                .ToListAsync(cancellationToken)).ToHashSet();

            var movimientosDto = movimientosDb.Select(x => new MovimientoCajaDto(
                x.Id,
                x.Fecha,
                x.Detalle,
                x.TipoMovimiento == TipoMovimientoCaja.Ingreso ? x.Importe : null,
                x.TipoMovimiento == TipoMovimientoCaja.Egreso ? x.Importe : null,
                x.FacturaId,
                x.AsientoContableId.HasValue && asientosDeTransferencias.Contains(x.AsientoContableId.Value),
                x.ClienteComercialId,
                x.ProveedorId
            )).ToList();

            var saldoAnterior = 0m;
            var totalIngresos = movimientosDb.Where(x => x.TipoMovimiento == TipoMovimientoCaja.Ingreso).Sum(x => x.Importe);
            var totalEgresos = movimientosDb.Where(x => x.TipoMovimiento == TipoMovimientoCaja.Egreso).Sum(x => x.Importe);
            var saldoActual = saldoAnterior + totalIngresos - totalEgresos;

            var resumen = new ResumenCajaDto(
                saldoAnterior,
                totalIngresos,
                totalEgresos,
                saldoActual,
                movimientosDto
            );

            return resumen;
        }
    }
}
