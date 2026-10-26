using EstudioContable.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EstudioContable.Infrastructure.Persistence.Configurations
{
    public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
    {
        public void Configure(EntityTypeBuilder<Usuario> builder)
        {
            builder.ToTable("Usuario", "contabilidad");

            builder.Property(u => u.NombreCompleto).HasMaxLength(150).IsRequired();
            builder.Property(u => u.Email).HasMaxLength(254).IsRequired();
            builder.Property(u => u.PasswordHash).IsRequired();
            builder.Property(u => u.Telefono).HasMaxLength(50);

            // El email es el nombre de usuario para el login: no puede repetirse.
            // Se guarda normalizado en minúsculas (ver Usuario.NormalizarEmail).
            builder.HasIndex(u => u.Email).IsUnique();
        }
    }
}
