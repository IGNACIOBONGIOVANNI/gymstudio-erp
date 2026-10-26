using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Banco.Queries.DTOs;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Banco.Queries.ObtenerLibroBanco
{
    public class ObtenerLibroBancoQueryHandler : IRequestHandler<ObtenerLibroBancoQuery, ResumenBancoDto>
    {
        private readonly IAppDbContext _context;

        public ObtenerLibroBancoQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<ResumenBancoDto> Handle(ObtenerLibroBancoQuery request, CancellationToken cancellationToken)
        {
            var movimientosDb = await _context.MovimientosBanco
                .Where(x => x.ClienteId == request.ClienteId)
                .OrderBy(x => x.Fecha)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

            // Asientos compartidos con el otro libro = transferencias entre Caja y Banco.
            var asientosDeTransferencias = (await _context.MovimientosCaja
                .Where(m => m.AsientoContableId != null)
                .Select(m => m.AsientoContableId!.Value)
                .ToListAsync(cancellationToken)).ToHashSet();

            var movimientosDto = movimientosDb.Select(x => new MovimientoBancoDto(
                x.Id,
                x.Fecha,
                x.Detalle,
                x.TipoMovimiento == TipoMovimientoBanco.Ingreso ? x.Importe : null,
                x.TipoMovimiento == TipoMovimientoBanco.Egreso ? x.Importe : null,
                x.FacturaId,
                x.AsientoContableId.HasValue && asientosDeTransferencias.Contains(x.AsientoContableId.Value)
            )).ToList();

            var saldoAnterior = 0m;
            var totalIngresos = movimientosDb.Where(x => x.TipoMovimiento == TipoMovimientoBanco.Ingreso).Sum(x => x.Importe);
            var totalEgresos = movimientosDb.Where(x => x.TipoMovimiento == TipoMovimientoBanco.Egreso).Sum(x => x.Importe);
            var saldoActual = saldoAnterior + totalIngresos - totalEgresos;

            var resumen = new ResumenBancoDto(
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
