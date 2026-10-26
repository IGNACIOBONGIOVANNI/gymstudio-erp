using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Common;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BalanceController : ControllerBase
    {
        private readonly IAppDbContext _context;

        public BalanceController(IAppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Balance general entre fechas. Las sumas y saldos por cuenta los calcula la
        /// función contabilidad.fn_sumas_y_saldos en PostgreSQL; acá sólo se agrupan
        /// por naturaleza para armar la respuesta.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetBalance([FromQuery] System.DateOnly? fechaInicio, [FromQuery] System.DateOnly? fechaFin, CancellationToken cancellationToken)
        {
            var cuentas = await _context
                .SumasYSaldos(PlanDeCuentas.ClienteIdPorDefecto, fechaInicio, fechaFin)
                .Where(c => c.SaldoNatural != 0)
                .ToListAsync(cancellationToken);

            object Composicion(NaturalezaCuenta naturaleza) => cuentas
                .Where(c => c.Naturaleza == naturaleza)
                .OrderBy(c => c.Codigo)
                .Select(c => new { name = c.Nombre, value = c.SaldoNatural })
                .ToList();

            decimal Total(NaturalezaCuenta naturaleza) => cuentas
                .Where(c => c.Naturaleza == naturaleza)
                .Sum(c => c.SaldoNatural);

            return Ok(new
            {
                totalActivo = Total(NaturalezaCuenta.Activo),
                totalPasivo = Total(NaturalezaCuenta.Pasivo),
                totalPatrimonioNeto = Total(NaturalezaCuenta.PatrimonioNeto),
                // Ingresos suman (saldo acreedor) y gastos restan (saldo deudor).
                resultadoDelEjercicio = Total(NaturalezaCuenta.Resultado),
                composicionActivos = Composicion(NaturalezaCuenta.Activo),
                composicionPasivos = Composicion(NaturalezaCuenta.Pasivo),
                composicionPatrimonioNeto = Composicion(NaturalezaCuenta.PatrimonioNeto),
                detalleResultados = Composicion(NaturalezaCuenta.Resultado)
            });
        }
    }
}
