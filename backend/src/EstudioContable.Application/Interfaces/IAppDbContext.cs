using System.Threading;
using System.Threading.Tasks;
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
        
        Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
