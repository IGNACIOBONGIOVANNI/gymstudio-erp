using EstudioContable.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EstudioContable.Infrastructure.Persistence.Configurations
{
    public class LineaAsientoConfiguration : IEntityTypeConfiguration<LineaAsiento>
    {
        public void Configure(EntityTypeBuilder<LineaAsiento> builder)
        {
            builder.ToTable("lineas_asiento", "contabilidad");
            builder.HasKey(l => l.Id);

            builder.Property(l => l.AsientoId).HasColumnName("asiento_id").IsRequired();
            builder.Property(l => l.CuentaId).HasColumnName("cuenta_id").IsRequired();
            builder.Property(l => l.Descripcion).HasColumnName("descripcion").HasMaxLength(300);
            builder.Property(l => l.Orden).HasColumnName("orden");

            // *** MAPEO CRÍTICO DE PRECISIÓN NUMÉRICA ***
            // numeric(18,2): 18 dígitos totales, 2 decimales. Coincide exactamente
            // con el tipo `decimal` de C# (punto fijo) y con el NUMERIC de la
            // tabla en el script SQL. Nunca usar "float8"/"double precision" aquí:
            // introduciría errores de redondeo en operaciones contables.
            builder.Property(l => l.Debe)
                .HasColumnName("debe")
                .HasColumnType("numeric(18,2)")
                .IsRequired();

            builder.Property(l => l.Haber)
                .HasColumnName("haber")
                .HasColumnType("numeric(18,2)")
                .IsRequired();

            // La FK a plan_de_cuentas se restringe (no cascade): no se debe poder
            // borrar una cuenta contable que ya tiene movimientos imputados.
            builder.HasOne(l => l.Cuenta)
                .WithMany()
                .HasForeignKey(l => l.CuentaId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(l => l.AsientoId);
            builder.HasIndex(l => l.CuentaId);
        }
    }
}
