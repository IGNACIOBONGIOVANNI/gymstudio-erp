using System;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.API.Auth;
using EstudioContable.Application.Common.Exceptions;
using EstudioContable.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EstudioContable.API.Controllers
{
    /// <summary>
    /// Cuentas a cobrar (clientes) y a pagar (proveedores). El cálculo está en <see cref="ICuentasCorrientesService"/>;
    /// acá solo se exponen los datos, y solo a Administrador y Contador. El total se compara con el saldo de la
    /// cuenta del Libro Mayor.
    /// </summary>
    [ApiController]
    [Route("api/cuentas-corrientes")]
    [Authorize(Roles = Roles.Gestion)]
    public class CuentasCorrientesController : ControllerBase
    {
        private readonly ICuentasCorrientesService _cuentas;

        public CuentasCorrientesController(ICuentasCorrientesService cuentas) => _cuentas = cuentas;

        private static DateOnly Hoy(DateOnly? hoy)
        {
            var dia = hoy ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3)); // Argentina
            if (dia.Year < 2000 || dia.Year > 2100) throw new ArgumentException("La fecha de hoy debe estar entre los años 2000 y 2100.");
            return dia;
        }

        private static bool? EsDeClientes(string tipo) => tipo.ToLowerInvariant() switch
        {
            "clientes" => true,
            "proveedores" => false,
            _ => null
        };

        [HttpGet("{tipo}")]
        public async Task<IActionResult> Listado(string tipo, [FromQuery] DateOnly? hoy, CancellationToken cancellationToken)
        {
            if (EsDeClientes(tipo) is not bool clientes) return NotFound();
            var l = await _cuentas.ListadoAsync(clientes, Hoy(hoy), cancellationToken);

            return Ok(new
            {
                Tipo = clientes ? "clientes" : "proveedores",
                l.Hoy,
                Total = new { l.Facturado, l.Pagado, l.Saldo, l.AFavor, l.Antiguedad },
                l.Terceros,
                // Control contra la contabilidad: el saldo de Deudores por Ventas (o de Proveedores) en el Mayor
                // debe ser igual al total. Si no, hay cobros o pagos cargados sin cliente/proveedor, o asientos manuales.
                Mayor = new { Cuenta = l.CuentaMayor, Saldo = l.SaldoMayor, Diferencia = l.SaldoMayor - l.Saldo }
            });
        }

        [HttpGet("{tipo}/{id:long}")]
        public async Task<IActionResult> Detalle(string tipo, long id, [FromQuery] DateOnly? hoy, CancellationToken cancellationToken)
        {
            if (EsDeClientes(tipo) is not bool clientes) return NotFound();
            var dia = Hoy(hoy);
            var d = await _cuentas.DetalleAsync(clientes, id, dia, cancellationToken)
                ?? throw new NotFoundException(clientes ? "Cliente" : "Proveedor", id);

            return Ok(new
            {
                Tercero = new { d.Id, d.Nombre, d.Cuit, d.CondicionIva },
                Hoy = dia,
                d.Resultado.Facturado, d.Resultado.Pagado, d.Resultado.Saldo, d.Resultado.AFavor,
                d.Resultado.Antiguedad, d.Resultado.Pendientes, d.Resultado.Estado
            });
        }
    }
}
