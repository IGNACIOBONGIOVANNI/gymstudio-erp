using System.Linq;
using System.Threading.Tasks;
using EstudioContable.Domain.Entities;
using EstudioContable.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EstudioContable.API.Auth
{
    /// <summary>
    /// Crea los usuarios de demostración la primera vez que arranca el sistema
    /// (tabla de usuarios vacía). Si ya existe algún usuario no hace nada, así que
    /// nunca pisa contraseñas cambiadas desde la pantalla de Usuarios.
    /// La contraseña inicial se lee de "UsuariosIniciales:Password" (ver README).
    /// </summary>
    public static class UsuariosIniciales
    {
        public static async Task CrearSiNoExistenAsync(AppDbContext context, IPasswordHasher<Usuario> hasher, IConfiguration configuration)
        {
            if (await context.Usuarios.AnyAsync())
                return;

            var password = configuration["UsuariosIniciales:Password"];
            if (string.IsNullOrWhiteSpace(password))
                return;

            var usuarios = new[]
            {
                new Usuario("Administrador Principal", "jefe@gym.com", "-", RolUsuario.Administrador,
                    telefono: "+54 1155550101", esPrincipal: true),
                new Usuario("Lic. Roberto Gómez", "contador@gymstudio.com", "-", RolUsuario.Contador,
                    telefono: "+54 1155550202"),
                new Usuario("Camila Fernández", "recepcion@gymstudio.com", "-", RolUsuario.Recepcion,
                    telefono: "+54 1155550303"),
            };

            foreach (var usuario in usuarios)
                usuario.CambiarPassword(hasher.HashPassword(usuario, password));

            context.Usuarios.AddRange(usuarios);
            await context.SaveChangesAsync();
        }
    }
}
