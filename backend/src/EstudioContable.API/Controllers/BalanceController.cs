using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
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

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetBalance([FromQuery] System.DateOnly? fechaInicio, [FromQuery] System.DateOnly? fechaFin, CancellationToken cancellationToken)
        {
            var queryLineas = _context.LineasAsiento
                .Where(l => l.Asiento.Estado == EstadoAsiento.Confirmado);

            if (fechaInicio.HasValue)
                queryLineas = queryLineas.Where(l => l.Asiento.Fecha >= fechaInicio.Value);

            if (fechaFin.HasValue)
                queryLineas = queryLineas.Where(l => l.Asiento.Fecha <= fechaFin.Value);

            var cuentasRaw = await _context.CuentasContables
                .Select(c => new
                {
                    Cuenta = c,
                    TotalDebe = queryLineas.Where(l => l.CuentaId == c.Id).Sum(l => (decimal?)l.Debe) ?? 0,
                    TotalHaber = queryLineas.Where(l => l.CuentaId == c.Id).Sum(l => (decimal?)l.Haber) ?? 0
                })
                .ToListAsync(cancellationToken);

            // 1. Agrupacion por cuenta: Unificar cuentas duplicadas (por nombre) y 2. Naturaleza inmutable
            var cuentasConSaldos = cuentasRaw
                .GroupBy(c => c.Cuenta.Nombre.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => {
                    var nombre = g.Key;
                    var esCajaOBanco = nombre.Contains("Caja", StringComparison.OrdinalIgnoreCase) || nombre.Contains("Banco", StringComparison.OrdinalIgnoreCase);
                    
                    return new {
                        Cuenta = new { 
                            Nombre = g.First().Cuenta.Nombre, 
                            Naturaleza = esCajaOBanco ? NaturalezaCuenta.Activo : g.First().Cuenta.Naturaleza 
                        },
                        TotalDebe = g.Sum(x => x.TotalDebe),
                        TotalHaber = g.Sum(x => x.TotalHaber)
                    };
                }).ToList();

            var activos = cuentasConSaldos
                .Where(x => x.Cuenta.Naturaleza == NaturalezaCuenta.Activo)
                .Select(x => new { name = x.Cuenta.Nombre, value = x.TotalDebe - x.TotalHaber })
                .Where(x => x.value != 0) // Omitimos saldos negativos (que serian anomalias) o ceros
                .ToList();

            var pasivos = cuentasConSaldos
                .Where(x => x.Cuenta.Naturaleza == NaturalezaCuenta.Pasivo)
                .Select(x => new { name = x.Cuenta.Nombre, value = x.TotalHaber - x.TotalDebe })
                .Where(x => x.value != 0)
                .ToList();

            var patrimonioNeto = cuentasConSaldos
                .Where(x => x.Cuenta.Naturaleza == NaturalezaCuenta.PatrimonioNeto)
                .Select(x => new { name = x.Cuenta.Nombre, value = x.TotalHaber - x.TotalDebe })
                .Where(x => x.value != 0)
                .ToList();

            // Para Resultado (Ingresos aumentan por Haber, Gastos aumentan por Debe)
            var resultados = cuentasConSaldos
                .Where(x => x.Cuenta.Naturaleza == NaturalezaCuenta.Resultado)
                .Select(x => new { name = x.Cuenta.Nombre, value = x.TotalHaber - x.TotalDebe })
                .Where(x => x.value != 0) // Aqui permitimos negativos para restar los Gastos del Resultado
                .ToList();

            decimal totalActivo = activos.Sum(x => x.value);
            decimal totalPasivo = pasivos.Sum(x => x.value);
            decimal totalPatrimonio = patrimonioNeto.Sum(x => x.value);
            decimal resultadoDelEjercicio = resultados.Sum(x => x.value);

            return Ok(new
            {
                totalActivo,
                totalPasivo,
                totalPatrimonioNeto = totalPatrimonio,
                resultadoDelEjercicio,
                composicionActivos = activos,
                composicionPasivos = pasivos,
                composicionPatrimonioNeto = patrimonioNeto,
                detalleResultados = resultados
            });
        }
    }
}

