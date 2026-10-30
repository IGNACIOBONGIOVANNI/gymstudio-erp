using EstudioContable.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EstudioContable.Infrastructure.Persistence.Configurations
{
    public class RegistroAuditoriaConfiguration : IEntityTypeConfiguration<RegistroAuditoria>
    {
        public void Configure(EntityTypeBuilder<RegistroAuditoria> builder)
        {
            builder.ToTable("auditoria", "contabilidad");
            builder.HasKey(a => a.Id);

            builder.Property(a => a.Id).HasColumnName("id");
            builder.Property(a => a.Tabla).HasColumnName("tabla").HasMaxLength(100).IsRequired();
            builder.Property(a => a.Operacion).HasColumnName("operacion").HasMaxLength(10).IsRequired();
            builder.Property(a => a.RegistroId).HasColumnName("registro_id").HasMaxLength(50);
            builder.Property(a => a.DatosAnteriores).HasColumnName("datos_anteriores").HasColumnType("jsonb");
            builder.Property(a => a.DatosNuevos).HasColumnName("datos_nuevos").HasColumnType("jsonb");
            builder.Property(a => a.CamposModificados).HasColumnName("campos_modificados").HasColumnType("text[]");
            builder.Property(a => a.Usuario).HasColumnName("usuario").HasMaxLength(254).IsRequired();
            builder.Property(a => a.Fecha).HasColumnName("fecha").HasDefaultValueSql("now()");
            builder.Property(a => a.Ip).HasColumnName("ip").HasMaxLength(45);
            builder.Property(a => a.SistemaOperativo).HasColumnName("sistema_operativo").HasMaxLength(60);
            builder.Property(a => a.Navegador).HasColumnName("navegador").HasMaxLength(60);

            builder.HasIndex(a => a.Fecha);
            builder.HasIndex(a => new { a.Tabla, a.RegistroId });
        }
    }
}
