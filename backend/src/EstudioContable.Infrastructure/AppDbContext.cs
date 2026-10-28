using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Enums;
using EstudioContable.Domain.Exceptions;
using EstadoAsiento = EstudioContable.Domain.Enums.EstadoAsiento;
using Microsoft.EntityFrameworkCore;

using EstudioContable.Application.Common.Lecturas;
using EstudioContable.Application.Interfaces;

namespace EstudioContable.Infrastructure.Persistence
{
    public class AppDbContext : DbContext, IAppDbContext
    {
        public DbSet<AsientoContable> AsientosContables => Set<AsientoContable>();
        public DbSet<LineaAsiento> LineasAsiento => Set<LineaAsiento>();
        public DbSet<CuentaContable> CuentasContables => Set<CuentaContable>();
        public DbSet<Usuario> Usuarios => Set<Usuario>();
        public DbSet<Cliente> Clientes => Set<Cliente>();
        public DbSet<FacturaVenta> FacturasVenta => Set<FacturaVenta>();
        public DbSet<FacturaCompra> FacturasCompra => Set<FacturaCompra>();
        public DbSet<MovimientoCaja> MovimientosCaja => Set<MovimientoCaja>();
        public DbSet<MovimientoBanco> MovimientosBanco => Set<MovimientoBanco>();
        public DbSet<Socio> Socios => Set<Socio>();
        public DbSet<Plan> Planes => Set<Plan>();
        public DbSet<Membresia> Membresias => Set<Membresia>();
        public DbSet<Factura> Facturas => Set<Factura>();
        public DbSet<FacturaItem> FacturaItems => Set<FacturaItem>();
        public DbSet<Producto> Productos => Set<Producto>();
        public DbSet<MovimientoStock> MovimientosStock => Set<MovimientoStock>();
        public DbSet<FacturaDetalle> FacturaDetalles => Set<FacturaDetalle>();
        public DbSet<ClienteComercial> ClientesComerciales => Set<ClienteComercial>();
        public DbSet<Proveedor> Proveedores => Set<Proveedor>();
        public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();
        public DbSet<Ejercicio> Ejercicios => Set<Ejercicio>();
        public DbSet<Rutina> Rutinas => Set<Rutina>();
        public DbSet<RutinaEjercicio> RutinaEjercicios => Set<RutinaEjercicio>();
        public DbSet<AsignacionRutina> AsignacionesRutina => Set<AsignacionRutina>();
        public DbSet<MedicionSocio> MedicionesSocio => Set<MedicionSocio>();

        public IQueryable<SaldoCuentaMayor> LibroMayor => Set<SaldoCuentaMayor>().AsNoTracking();

        public IQueryable<FilaSumasYSaldos> SumasYSaldos(long clienteId, System.DateOnly? desde, System.DateOnly? hasta)
            => Set<FilaSumasYSaldos>()
                .FromSqlInterpolated($"SELECT * FROM contabilidad.fn_sumas_y_saldos({clienteId}, {desde}, {hasta})")
                .AsNoTracking();

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("contabilidad");

            // Aplica todas las clases IEntityTypeConfiguration<T> del assembly
            // (AsientoContableConfiguration, LineaAsientoConfiguration, etc.),
            // manteniendo el mapeo desacoplado de las entidades de dominio.
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            modelBuilder.Entity<Factura>()
                .HasMany(f => f.Items)
                .WithOne(i => i.Factura)
                .HasForeignKey(i => i.FacturaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Sin esto EF no asocia PlanActualObj con PlanActualId (no sigue la convención
            // de nombres) y crea una FK oculta "PlanActualObjId" que nunca se completa.
            modelBuilder.Entity<Socio>()
                .HasOne(s => s.PlanActualObj)
                .WithMany()
                .HasForeignKey(s => s.PlanActualId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Producto>().ToTable("Productos", "inventario", t =>
                t.HasCheckConstraint("CK_Productos_StockNoNegativo", "\"StockActual\" >= 0"));

            // Integridad de importes también a nivel base de datos (además del dominio).
            // La tabla se llama "Cliente" desde la migración inicial; el DbSet Clientes no la renombra.
            modelBuilder.Entity<Cliente>().ToTable("Cliente");

            // Integridad referencial que faltaba: la factura apunta a su asiento y el
            // ítem a su producto. Restrict: no se puede borrar algo referenciado.
            modelBuilder.Entity<Factura>()
                .HasOne<AsientoContable>()
                .WithMany()
                .HasForeignKey(f => f.AsientoContableId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FacturaItem>()
                .HasOne<Producto>()
                .WithMany()
                .HasForeignKey(i => i.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Factura>().Ignore(f => f.NumeroFormateado);
            modelBuilder.Entity<FacturaItem>().Ignore(i => i.AgregarAlInventario);

            modelBuilder.Entity<Factura>().ToTable(t =>
            {
                t.HasCheckConstraint("CK_Facturas_ImportesNoNegativos", "\"Subtotal\" >= 0 AND \"Iva\" >= 0");
                t.HasCheckConstraint("CK_Facturas_TotalEsSubtotalMasIva", "\"Total\" = \"Subtotal\" + \"Iva\"");
            });

            modelBuilder.Entity<Plan>().ToTable(t =>
                t.HasCheckConstraint("CK_Planes_Valores", "\"DuracionMeses\" > 0 AND \"PrecioMensual\" >= 0"));

            // Rutinas de gimnasio. Los CHECK repiten las reglas del dominio; el índice parcial
            // garantiza una sola rutina activa por socio aunque dos recepcionistas asignen a la vez.
            modelBuilder.Entity<Ejercicio>(e =>
            {
                e.Property(x => x.Nombre).HasMaxLength(100);
                e.Property(x => x.GrupoMuscular).HasMaxLength(30);
            });

            modelBuilder.Entity<Rutina>(e =>
            {
                e.Property(x => x.Nombre).HasMaxLength(100);
                e.Property(x => x.Objetivo).HasMaxLength(200);
                e.Property(x => x.Nivel).HasMaxLength(20);
                e.HasMany(x => x.Ejercicios).WithOne().HasForeignKey(x => x.RutinaId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<RutinaEjercicio>(e =>
            {
                e.Property(x => x.Repeticiones).HasMaxLength(20);
                e.Property(x => x.Indicaciones).HasMaxLength(200);
                e.HasOne(x => x.Ejercicio).WithMany().HasForeignKey(x => x.EjercicioId).OnDelete(DeleteBehavior.Restrict);
                e.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_RutinaEjercicios_Dia", "\"Dia\" BETWEEN 1 AND 7");
                    t.HasCheckConstraint("CK_RutinaEjercicios_Series", "\"Series\" BETWEEN 1 AND 20");
                    t.HasCheckConstraint("CK_RutinaEjercicios_Descanso", "\"DescansoSegundos\" BETWEEN 0 AND 600");
                });
            });

            modelBuilder.Entity<AsignacionRutina>(e =>
            {
                e.Property(x => x.Observaciones).HasMaxLength(500);
                e.HasOne(x => x.Socio).WithMany().HasForeignKey(x => x.SocioId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Rutina).WithMany().HasForeignKey(x => x.RutinaId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => x.SocioId).IsUnique().HasFilter("\"Activa\"").HasDatabaseName("ux_asignaciones_rutina_activa_por_socio");
                e.ToTable(t => t.HasCheckConstraint("CK_AsignacionesRutina_Fechas", "\"FechaFin\" IS NULL OR \"FechaFin\" >= \"FechaInicio\""));
            });

            // Seguimiento de peso y medidas: una medición por socio y día, con rangos razonables en la base.
            modelBuilder.Entity<MedicionSocio>(e =>
            {
                e.Property(x => x.Notas).HasMaxLength(300);
                foreach (var propiedad in new[] { nameof(MedicionSocio.PesoKg), nameof(MedicionSocio.GrasaCorporalPct), nameof(MedicionSocio.CinturaCm),
                    nameof(MedicionSocio.CaderaCm), nameof(MedicionSocio.PechoCm), nameof(MedicionSocio.BrazoCm), nameof(MedicionSocio.MusloCm) })
                    e.Property(propiedad).HasPrecision(6, 1);
                e.HasOne<Socio>().WithMany().HasForeignKey(x => x.SocioId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.SocioId, x.Fecha }).IsUnique().HasDatabaseName("ux_mediciones_socio_fecha");
                e.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_MedicionesSocio_Peso", "\"PesoKg\" BETWEEN 20 AND 400");
                    t.HasCheckConstraint("CK_MedicionesSocio_Grasa", "\"GrasaCorporalPct\" IS NULL OR \"GrasaCorporalPct\" BETWEEN 2 AND 70");
                    t.HasCheckConstraint("CK_MedicionesSocio_Perimetros",
                        "(\"CinturaCm\" IS NULL OR \"CinturaCm\" BETWEEN 30 AND 250) AND (\"CaderaCm\" IS NULL OR \"CaderaCm\" BETWEEN 30 AND 250) AND " +
                        "(\"PechoCm\" IS NULL OR \"PechoCm\" BETWEEN 30 AND 250) AND (\"BrazoCm\" IS NULL OR \"BrazoCm\" BETWEEN 10 AND 100) AND " +
                        "(\"MusloCm\" IS NULL OR \"MusloCm\" BETWEEN 20 AND 150)");
                });
            });

            // Cobros y pagos a cuenta corriente: el cliente o proveedor al que corresponden. Un movimiento
            // nunca es de los dos a la vez, y no se puede borrar un cliente o proveedor que tiene cobros o pagos.
            modelBuilder.Entity<MovimientoCaja>(e =>
            {
                e.HasOne<ClienteComercial>().WithMany().HasForeignKey(x => x.ClienteComercialId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Proveedor>().WithMany().HasForeignKey(x => x.ProveedorId).OnDelete(DeleteBehavior.Restrict);
                e.ToTable(t => t.HasCheckConstraint("CK_movimientos_caja_un_solo_tercero", "NOT (\"ClienteComercialId\" IS NOT NULL AND \"ProveedorId\" IS NOT NULL)"));
            });
            modelBuilder.Entity<MovimientoBanco>(e =>
            {
                e.HasOne<ClienteComercial>().WithMany().HasForeignKey(x => x.ClienteComercialId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Proveedor>().WithMany().HasForeignKey(x => x.ProveedorId).OnDelete(DeleteBehavior.Restrict);
                e.ToTable(t => t.HasCheckConstraint("CK_movimientos_banco_un_solo_tercero", "NOT (\"ClienteComercialId\" IS NOT NULL AND \"ProveedorId\" IS NOT NULL)"));
            });

            modelBuilder.Entity<Membresia>().ToTable(t =>
                t.HasCheckConstraint("CK_Membresias_Vigencia", "\"FechaVencimiento\" >= \"FechaInicio\""));

            // Lecturas que resuelve la base de datos: una vista y una función.
            // Son entidades sin clave y quedan fuera de las migraciones de tablas: la vista
            // y la función se crean con SQL en la migración ObjetosDeBaseDeDatos.
            modelBuilder.Entity<SaldoCuentaMayor>(e =>
            {
                e.HasNoKey().ToView("v_libro_mayor", "contabilidad");
                e.Property(x => x.ClienteId).HasColumnName("cliente_id");
                e.Property(x => x.CuentaId).HasColumnName("cuenta_id");
                e.Property(x => x.Codigo).HasColumnName("codigo");
                e.Property(x => x.Nombre).HasColumnName("nombre");
                e.Property(x => x.Naturaleza).HasColumnName("naturaleza");
                e.Property(x => x.TotalDebe).HasColumnName("total_debe");
                e.Property(x => x.TotalHaber).HasColumnName("total_haber");
                e.Property(x => x.Saldo).HasColumnName("saldo");
            });

            modelBuilder.Entity<FilaSumasYSaldos>(e =>
            {
                e.HasNoKey().ToView(null);
                e.Property(x => x.CuentaId).HasColumnName("cuenta_id");
                e.Property(x => x.Codigo).HasColumnName("codigo");
                e.Property(x => x.Nombre).HasColumnName("nombre");
                e.Property(x => x.Naturaleza).HasColumnName("naturaleza");
                e.Property(x => x.TotalDebe).HasColumnName("total_debe");
                e.Property(x => x.TotalHaber).HasColumnName("total_haber");
                e.Property(x => x.SaldoNatural).HasColumnName("saldo_natural");
            });
            modelBuilder.Entity<MovimientoStock>().ToTable("MovimientosStock", "inventario");

            modelBuilder.Entity<FacturaDetalle>().ToTable("FacturaDetalles", "contabilidad");

            modelBuilder.Entity<FacturaVenta>()
                .HasMany(f => f.Detalles)
                .WithOne()
                .HasForeignKey(d => d.FacturaVentaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FacturaCompra>()
                .HasMany(f => f.Detalles)
                .WithOne()
                .HasForeignKey(d => d.FacturaCompraId)
                .OnDelete(DeleteBehavior.Cascade);

            // Data Seeding
            modelBuilder.Entity<Cliente>().HasData(
                new { 
                    Id = 1L, 
                    RazonSocial = "Cliente Demo", 
                    Cuit = "20111111112", 
                    TipoContribuyente = TipoContribuyente.ResponsableInscripto,
                    Activo = true,
                    FechaAlta = new System.DateTimeOffset(2026, 1, 1, 0, 0, 0, System.TimeSpan.Zero)
                }
            );

            modelBuilder.Entity<CuentaContable>().HasData(
                new { Id = 101L, ClienteId = 1L, Codigo = "1.1.1.01", Nombre = "Caja", Naturaleza = NaturalezaCuenta.Activo, Activa = true },
                new { Id = 102L, ClienteId = 1L, Codigo = "1.1.1.02", Nombre = "Banco", Naturaleza = NaturalezaCuenta.Activo, Activa = true },
                new { Id = 103L, ClienteId = 1L, Codigo = "1.1.2.01", Nombre = "Deudores por Ventas", Naturaleza = NaturalezaCuenta.Activo, Activa = true },
                new { Id = 104L, ClienteId = 1L, Codigo = "4.1.1.01", Nombre = "Ventas", Naturaleza = NaturalezaCuenta.Resultado, Activa = true },
                new { Id = 105L, ClienteId = 1L, Codigo = "1.1.2.02", Nombre = "IVA Crédito Fiscal", Naturaleza = NaturalezaCuenta.Activo, Activa = true },
                // Contrapartidas de los movimientos manuales de Caja y Banco.
                new { Id = 106L, ClienteId = 1L, Codigo = "4.1.2.01", Nombre = "Otros Ingresos", Naturaleza = NaturalezaCuenta.Resultado, Activa = true },
                new { Id = 107L, ClienteId = 1L, Codigo = "5.2.1.01", Nombre = "Gastos Generales", Naturaleza = NaturalezaCuenta.Resultado, Activa = true },
                new { Id = 201L, ClienteId = 1L, Codigo = "2.1.1.01", Nombre = "IVA Débito Fiscal", Naturaleza = NaturalezaCuenta.Pasivo, Activa = true },
                new { Id = 202L, ClienteId = 1L, Codigo = "2.1.1.02", Nombre = "Proveedores", Naturaleza = NaturalezaCuenta.Pasivo, Activa = true },
                new { Id = 501L, ClienteId = 1L, Codigo = "5.1.1.01", Nombre = "Compras", Naturaleza = NaturalezaCuenta.Resultado, Activa = true }
            );

            modelBuilder.Entity<Producto>().HasData(
                new { Id = 1L, Nombre = "Producto A", PrecioUnitario = 100m, PrecioVenta = 150m, StockActual = 100 },
                new { Id = 2L, Nombre = "Producto B", PrecioUnitario = 200m, PrecioVenta = 300m, StockActual = 50 },
                new { Id = 3L, Nombre = "Servicio C", PrecioUnitario = 0m, PrecioVenta = 500m, StockActual = 0 }
            );

            base.OnModelCreating(modelBuilder);
        }

        /// <summary>
        /// Red de seguridad final: aunque el dominio ya impide confirmar un asiento
        /// desbalanceado (AsientoContable.Confirmar), esta verificación evita que
        /// un asiento marcado Confirmado llegue desbalanceado a la base de datos
        /// por cualquier otro camino (ej. una línea modificada tras confirmar).
        /// </summary>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var asientosConfirmados = ChangeTracker.Entries<AsientoContable>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
                .Select(e => e.Entity)
                .Where(a => (int)a.Estado == (int)EstadoAsiento.Confirmado);

            foreach (var asiento in asientosConfirmados)
            {
                if (!asiento.EstaBalanceado)
                    throw new AsientoDesbalanceadoException(asiento.TotalDebe, asiento.TotalHaber);
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
