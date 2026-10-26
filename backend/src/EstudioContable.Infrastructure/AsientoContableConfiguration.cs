using EstudioContable.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EstudioContable.Infrastructure.Persistence.Configurations
{
    public class AsientoContableConfiguration : IEntityTypeConfiguration<AsientoContable>
    {
        public void Configure(EntityTypeBuilder<AsientoContable> builder)
        {
            builder.ToTable("asientos_contables", "contabilidad");
            builder.HasKey(a => a.Id);

            builder.Property(a => a.ClienteId).HasColumnName("cliente_id").IsRequired();
            builder.Property(a => a.NumeroAsiento).HasColumnName("numero_asiento").IsRequired();
            builder.Property(a => a.Fecha).HasColumnName("fecha").HasColumnType("date").IsRequired();
            builder.Property(a => a.Descripcion).HasColumnName("descripcion").HasMaxLength(500).IsRequired();
            builder.Property(a => a.UsuarioCreadorId).HasColumnName("usuario_creador_id").IsRequired();
            builder.Property(a => a.FechaCreacion).HasColumnName("fecha_creacion");
            builder.Property(a => a.FechaConfirmacion).HasColumnName("fecha_confirmacion");
            builder.Property(a => a.UsuarioConfirmacionId).HasColumnName("usuario_confirmacion_id");

            // Enum persistido como texto (más legible en la DB que un int);
            // evita romper datos existentes si se reordena el enum a futuro.
            builder.Property(a => a.Estado)
                .HasColumnName("estado")
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            // Un mismo cliente no puede repetir número de asiento.
            builder.HasIndex(a => new { a.ClienteId, a.NumeroAsiento }).IsUnique();
            builder.HasIndex(a => new { a.ClienteId, a.Fecha });

            // Acceso a la colección privada de líneas vía el campo de respaldo (_lineas),
            // preservando el encapsulamiento del agregado (no se expone un setter público).
            builder.Metadata
                .FindNavigation(nameof(AsientoContable.Lineas))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(a => a.Lineas)
                .WithOne(l => l.Asiento)
                .HasForeignKey(l => l.AsientoId)
                .OnDelete(DeleteBehavior.Cascade);

            // Propiedades calculadas en memoria: nunca se mapean a columnas.
            builder.Ignore(a => a.TotalDebe);
            builder.Ignore(a => a.TotalHaber);
            builder.Ignore(a => a.EstaBalanceado);
        }
    }
}
