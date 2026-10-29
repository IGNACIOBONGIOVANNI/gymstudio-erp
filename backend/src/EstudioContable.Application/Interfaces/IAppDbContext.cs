using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using EstudioContable.Application.Common.Lecturas;
using EstudioContable.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Interfaces
{
    public interface IAppDbContext
    {
        DbSet<AsientoContable> AsientosContables { get; }
        DbSet<LineaAsiento> LineasAsiento { get; }
        DbSet<CuentaContable> CuentasContables { get; }
        DbSet<Usuario> Usuarios { get; }
        DbSet<Cliente> Clientes { get; }
        DbSet<FacturaVenta> FacturasVenta { get; }
        DbSet<FacturaCompra> FacturasCompra { get; }
        DbSet<MovimientoCaja> MovimientosCaja { get; }
        DbSet<MovimientoBanco> MovimientosBanco { get; }
        DbSet<Socio> Socios { get; }
        DbSet<Plan> Planes { get; }
        DbSet<Membresia> Membresias { get; }
        DbSet<Factura> Facturas { get; }
        DbSet<FacturaItem> FacturaItems { get; }
        DbSet<Producto> Productos { get; }
        DbSet<MovimientoStock> MovimientosStock { get; }
        DbSet<FacturaDetalle> FacturaDetalles { get; }
        DbSet<ClienteComercial> ClientesComerciales { get; }
        DbSet<Proveedor> Proveedores { get; }
        DbSet<RegistroAuditoria> Auditoria { get; }
        DbSet<Ejercicio> Ejercicios { get; }
        DbSet<Rutina> Rutinas { get; }
        DbSet<RutinaEjercicio> RutinaEjercicios { get; }
        DbSet<AsignacionRutina> AsignacionesRutina { get; }
        DbSet<MedicionSocio> MedicionesSocio { get; }
        DbSet<AsistenciaSocio> AsistenciasSocio { get; }

        /// <summary>Vista contabilidad.v_libro_mayor: saldos por cuenta de los asientos confirmados.</summary>
        IQueryable<SaldoCuentaMayor> LibroMayor { get; }

        /// <summary>Función contabilidad.fn_sumas_y_saldos: balance de sumas y saldos entre fechas.</summary>
        IQueryable<FilaSumasYSaldos> SumasYSaldos(long clienteId, System.DateOnly? desde, System.DateOnly? hasta);
        
        Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
