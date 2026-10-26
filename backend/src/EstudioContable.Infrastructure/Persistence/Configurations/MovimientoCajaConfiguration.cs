using EstudioContable.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace EstudioContable.Infrastructure.Persistence.Configurations
{
    public class MovimientoCajaConfiguration : IEntityTypeConfiguration<MovimientoCaja>
    {
        public void Configure(EntityTypeBuilder<MovimientoCaja> builder)
        {
            builder.ToTable("movimientos_caja", "caja");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.ClienteId)
                   .IsRequired()
                   .HasMaxLength(50);

            builder.Property(x => x.Fecha)
                   .IsRequired()
                   .HasColumnType("date");

            builder.Property(x => x.Detalle)
                   .IsRequired()
                   .HasMaxLength(255);

            builder.Property(x => x.TipoMovimiento)
                   .IsRequired();

            builder.Property(x => x.Importe)
                   .IsRequired()
                   .HasColumnType("decimal(18,2)");

            builder.HasOne<Factura>()
                   .WithMany()
                   .HasForeignKey(x => x.FacturaId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
