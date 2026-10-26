using System;
using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EstudioContable.Infrastructure.Persistence.Configurations
{
    public class FacturaVentaConfiguration : IEntityTypeConfiguration<FacturaVenta>
    {
        public void Configure(EntityTypeBuilder<FacturaVenta> builder)
        {
            builder.ToTable("facturas_venta", "ventas");
            builder.HasKey(f => f.Id);

            builder.Property(f => f.ClienteId).HasColumnName("cliente_id").IsRequired();
            builder.Property(f => f.Fecha).HasColumnName("fecha").HasColumnType("date").IsRequired();
            builder.Property(f => f.TipoFactura).HasColumnName("tipo_factura").HasMaxLength(3).IsRequired();
            builder.Property(f => f.NumeroFactura).HasColumnName("numero_factura").HasMaxLength(20).IsRequired();
            builder.Property(f => f.ClienteNombre).HasColumnName("cliente_nombre").HasMaxLength(200).IsRequired();
            builder.Property(f => f.Cuit).HasColumnName("cuit").HasMaxLength(20).IsRequired();
            
            builder.Property(f => f.Neto)
                .HasColumnName("neto")
                .HasColumnType("numeric(18,2)")
                .IsRequired();
                
            builder.Property(f => f.Iva)
                .HasColumnName("iva")
                .HasColumnType("numeric(18,2)")
                .IsRequired();
                
            builder.Property(f => f.Total)
                .HasColumnName("total")
                .HasColumnType("numeric(18,2)")
                .IsRequired();

        }
    }
}
