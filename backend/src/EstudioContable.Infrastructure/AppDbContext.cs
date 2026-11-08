using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Domain;
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
        public DbSet<AsistenciaSocio> AsistenciasSocio => Set<AsistenciaSocio>();
        public DbSet<TurnoEncargado> TurnosEncargado => Set<TurnoEncargado>();
        public DbSet<Entrenador> Entrenadores => Set<Entrenador>();
        public DbSet<FotoDeEntrenador> FotosDeEntrenador => Set<FotoDeEntrenador>();
        public DbSet<HoraDeEntrenador> HorasDeEntrenador => Set<HoraDeEntrenador>();
        public DbSet<ConfiguracionDeCopias> ConfiguracionesDeCopias => Set<ConfiguracionDeCopias>();

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

            // Ingresos al gimnasio: uno por socio y día (aunque pasen dos veces o dos recepcionistas lo carguen a la vez),
            // y no se puede borrar un socio que tiene ingresos.
            modelBuilder.Entity<AsistenciaSocio>(e =>
            {
                e.Property(x => x.RegistradaPor).HasMaxLength(100);
                e.HasOne<Socio>().WithMany().HasForeignKey(x => x.SocioId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.SocioId, x.Fecha }).IsUnique().HasDatabaseName("ux_asistencias_socio_fecha");
                e.HasIndex(x => x.Fecha).HasDatabaseName("ix_asistencias_fecha");
            });

            // Entrenadores: fichas sin acceso al sistema, para ponerlas en el calendario. El nombre no se repite sin importar las
            // mayúsculas: lo garantiza el índice único ux_entrenadores_nombre sobre lower("NombreCompleto"), que se crea en la
            // migración con SQL porque el modelo de EF no sabe describir un índice sobre una expresión.
            modelBuilder.Entity<Entrenador>(e =>
            {
                e.Property(x => x.NombreCompleto).HasMaxLength(Entrenador.LargoMaximoDelNombre).IsRequired();
                e.Property(x => x.Telefono).HasMaxLength(Entrenador.LargoMaximoDelTelefono);
                e.Property(x => x.Especialidad).HasMaxLength(Entrenador.LargoMaximoDeLaEspecialidad);
                e.Property(x => x.Email).HasMaxLength(Entrenador.LargoMaximoDelEmail);
                e.Property(x => x.WhatsApp).HasMaxLength(Entrenador.LargoMaximoDelWhatsApp);
                e.Property(x => x.ValorHora).HasPrecision(18, 2);
                e.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_Entrenadores_Nombre", "char_length(btrim(\"NombreCompleto\")) > 0");
                    // La misma forma que pide el dominio (algo@dominio.ext, sin espacios), por si alguien escribe directo en la base.
                    t.HasCheckConstraint("CK_Entrenadores_Email", "\"Email\" IS NULL OR \"Email\" ~ '^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$'");
                    // El valor de la hora: vacío (todavía sin valor) o de 0 al mismo tope que el resto del dinero del sistema.
                    t.HasCheckConstraint("CK_Entrenadores_ValorHora", "\"ValorHora\" IS NULL OR \"ValorHora\" BETWEEN 0 AND 999999999999");
                });
            });

            // Horas cumplidas por entrenador y día, con el valor de la hora de ese momento (no cambia si el de la ficha sube). Un registro
            // por entrenador y día, y no se puede borrar a un entrenador que tiene horas. La base repite las reglas del dominio
            // (ReglasDeHoras: de 0 a 24 horas y de a cuarto de hora) para que valgan aunque alguien escriba directo en ella. Tiene
            // disparador de auditoría, que se crea en la migración porque el modelo de EF no sabe describirlo.
            modelBuilder.Entity<HoraDeEntrenador>(e =>
            {
                e.Property(x => x.Horas).HasPrecision(4, 2);
                e.Property(x => x.ValorHora).HasPrecision(18, 2);
                e.Property(x => x.Nota).HasMaxLength(ReglasDeHoras.LargoMaximoDeLaNota);
                e.HasOne<Entrenador>().WithMany().HasForeignKey(x => x.EntrenadorId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.EntrenadorId, x.Fecha }).IsUnique().HasDatabaseName("ux_horas_entrenador_fecha");
                e.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_HorasDeEntrenador_Horas", "\"Horas\" BETWEEN 0 AND 24 AND \"Horas\" * 4 = trunc(\"Horas\" * 4)");
                    t.HasCheckConstraint("CK_HorasDeEntrenador_ValorHora", "\"ValorHora\" BETWEEN 0 AND 999999999999");
                });
            });

            // La foto de la ficha vive aparte (una por entrenador, JPEG de hasta 150 000 bytes): la lista y el calendario no cargan
            // imágenes, y la tabla no tiene disparador de auditoría para que cada foto no se copie entera en la bitácora (las altas y
            // bajas de foto se registran como eventos desde el controlador). Se va con su ficha si alguna vez se borra.
            modelBuilder.Entity<FotoDeEntrenador>(e =>
            {
                e.HasKey(x => x.EntrenadorId);
                e.Property(x => x.EntrenadorId).ValueGeneratedNever();
                e.Property(x => x.Foto).IsRequired();
                e.HasOne<Entrenador>().WithMany().HasForeignKey(x => x.EntrenadorId).OnDelete(DeleteBehavior.Cascade);
                e.ToTable(t => t.HasCheckConstraint("CK_FotosDeEntrenador_Tamano", "octet_length(\"Foto\") BETWEEN 100 AND 150000"));
            });

            // Calendario semanal de entrenadores a cargo: uno por día y hora, de lunes a viernes y de 06 a 22 hs. No se puede borrar
            // un entrenador que tiene turnos. La base repite las reglas del dominio (ReglasDeCalendario) para que valgan aunque
            // alguien escriba directo en ella.
            modelBuilder.Entity<TurnoEncargado>(e =>
            {
                e.HasOne(x => x.Entrenador).WithMany().HasForeignKey(x => x.EntrenadorId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.DiaSemana, x.Hora }).IsUnique().HasDatabaseName("ux_turnos_encargado_dia_hora");
                e.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_TurnosEncargado_Dia", $"\"DiaSemana\" BETWEEN {Domain.ReglasDeCalendario.PrimerDia} AND {Domain.ReglasDeCalendario.UltimoDia}");
                    t.HasCheckConstraint("CK_TurnosEncargado_Hora", $"\"Hora\" BETWEEN {Domain.ReglasDeCalendario.PrimeraHora} AND {Domain.ReglasDeCalendario.UltimaHora}");
                });
            });

            // Programación de las copias de seguridad automáticas: una sola fila (Id = 1). La base repite las reglas del dominio
            // (ConfiguracionDeCopias y ReglasDeCopiasAutomaticas) para que valgan aunque alguien escriba directo en ella.
            modelBuilder.Entity<ConfiguracionDeCopias>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
                // Se guarda el nombre de la frecuencia (legible en la copia de seguridad), no su número.
                e.Property(x => x.Frecuencia).HasConversion<string>().HasMaxLength(20);
                e.Property(x => x.UltimoError).HasMaxLength(ConfiguracionDeCopias.LargoMaximoDelError);
                var frecuencias = string.Join(", ", Enum.GetNames<FrecuenciaDeCopia>().Select(n => $"'{n}'"));
                e.ToTable("ConfiguracionDeCopias", t =>
                {
                    t.HasCheckConstraint("CK_ConfiguracionDeCopias_Unica", $"\"Id\" = {ConfiguracionDeCopias.IdUnico}");
                    t.HasCheckConstraint("CK_ConfiguracionDeCopias_Hora", "\"Hora\" BETWEEN 0 AND 23");
                    t.HasCheckConstraint("CK_ConfiguracionDeCopias_Minuto", "\"Minuto\" BETWEEN 0 AND 59");
                    t.HasCheckConstraint("CK_ConfiguracionDeCopias_Conservar", $"\"Conservar\" BETWEEN {ConfiguracionDeCopias.ConservarMinimo} AND {ConfiguracionDeCopias.ConservarMaximo}");
                    t.HasCheckConstraint("CK_ConfiguracionDeCopias_DiaSemana", "\"DiaDeLaSemana\" IS NULL OR \"DiaDeLaSemana\" BETWEEN 1 AND 7");
                    t.HasCheckConstraint("CK_ConfiguracionDeCopias_DiaMes", $"\"DiaDelMes\" IS NULL OR \"DiaDelMes\" BETWEEN 1 AND {ConfiguracionDeCopias.DiaDelMesMaximo}");
                    t.HasCheckConstraint("CK_ConfiguracionDeCopias_Frecuencia", $"\"Frecuencia\" IN ({frecuencias})");
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
