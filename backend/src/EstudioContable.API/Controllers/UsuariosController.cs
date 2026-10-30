using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.API.Auth;
using EstudioContable.Application.Common.Exceptions;
using EstudioContable.Domain;
using EstudioContable.Domain.Entities;
using EstudioContable.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.API.Controllers
{
    /// <summary>Alta, baja y modificación de usuarios. Sólo para administradores.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = Roles.Administrador)]
    public class UsuariosController : ControllerBase
    {

        private readonly AppDbContext _context;
        private readonly IPasswordHasher<Usuario> _hasher;

        public UsuariosController(AppDbContext context, IPasswordHasher<Usuario> hasher)
        {
            _context = context;
            _hasher = hasher;
        }

        public record UsuarioDto(
            long Id, string Nombre, string Email, string Rol, string? Telefono,
            bool Activo, bool EsPrincipal, DateTimeOffset? UltimoLogin, DateTimeOffset FechaCreacion);

        public record GuardarUsuarioRequest(
            string Nombre, string Email, string Rol, string? Telefono, bool Activo, string? Password);

        public record CambiarEstadoRequest(bool Activo);

        [HttpGet]
        public async Task<IActionResult> Listar(CancellationToken cancellationToken)
        {
            var usuarios = await _context.Usuarios
                .AsNoTracking()
                .OrderByDescending(u => u.EsPrincipal).ThenBy(u => u.NombreCompleto)
                .Select(u => new UsuarioDto(u.Id, u.NombreCompleto, u.Email, u.Rol.ToString(), u.Telefono,
                    u.Activo, u.EsPrincipal, u.UltimoLogin, u.FechaCreacion))
                .ToListAsync(cancellationToken);

            return Ok(usuarios);
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] GuardarUsuarioRequest request, CancellationToken cancellationToken)
        {
            ValidarPassword(request.Password, obligatoria: true);
            await ValidarEmailDisponibleAsync(request.Email, idExcluido: null, cancellationToken);

            var usuario = new Usuario(request.Nombre, request.Email, "-", ParsearRol(request.Rol), telefono: request.Telefono);
            usuario.CambiarPassword(_hasher.HashPassword(usuario, request.Password!));
            if (!request.Activo)
                usuario.Desactivar();

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync(cancellationToken);

            return Created($"/api/usuarios/{usuario.Id}", new { id = usuario.Id });
        }

        [HttpPut("{id:long}")]
        public async Task<IActionResult> Actualizar(long id, [FromBody] GuardarUsuarioRequest request, CancellationToken cancellationToken)
        {
            var usuario = await BuscarAsync(id, cancellationToken);
            ValidarPassword(request.Password, obligatoria: false);
            await ValidarEmailDisponibleAsync(request.Email, idExcluido: id, cancellationToken);

            usuario.ActualizarDatos(request.Nombre, request.Email, ParsearRol(request.Rol), request.Telefono);
            AplicarEstado(usuario, request.Activo);

            if (!string.IsNullOrWhiteSpace(request.Password))
                usuario.CambiarPassword(_hasher.HashPassword(usuario, request.Password));

            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        [HttpPut("{id:long}/estado")]
        public async Task<IActionResult> CambiarEstado(long id, [FromBody] CambiarEstadoRequest request, CancellationToken cancellationToken)
        {
            var usuario = await BuscarAsync(id, cancellationToken);
            AplicarEstado(usuario, request.Activo);
            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Eliminar(long id, CancellationToken cancellationToken)
        {
            var usuario = await BuscarAsync(id, cancellationToken);

            if (usuario.EsPrincipal)
                throw new InvalidOperationException("Operación bloqueada: el Administrador Principal no puede eliminarse.");
            if (usuario.Id == TokenService.ObtenerUsuarioId(User))
                throw new InvalidOperationException("No podés eliminar tu propio usuario.");

            _context.Usuarios.Remove(usuario);
            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        private void AplicarEstado(Usuario usuario, bool activo)
        {
            if (activo)
            {
                usuario.Activar();
                return;
            }

            if (usuario.Id == TokenService.ObtenerUsuarioId(User))
                throw new InvalidOperationException("No podés desactivar tu propio usuario.");

            usuario.Desactivar();
        }

        private async Task<Usuario> BuscarAsync(long id, CancellationToken cancellationToken)
        {
            return await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
                ?? throw new NotFoundException(nameof(Usuario), id);
        }

        private async Task ValidarEmailDisponibleAsync(string email, long? idExcluido, CancellationToken cancellationToken)
        {
            var normalizado = Usuario.NormalizarEmail(email);
            var enUso = await _context.Usuarios.AnyAsync(u => u.Email == normalizado && u.Id != idExcluido, cancellationToken);
            if (enUso)
                throw new InvalidOperationException($"Ya existe un usuario con el email {normalizado}.");
        }

        private static void ValidarPassword(string? password, bool obligatoria)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                if (obligatoria)
                    throw new ArgumentException("La contraseña es obligatoria.");
                return;
            }

            ReglasDeClave.Validar(password);
        }

        private static RolUsuario ParsearRol(string rol)
        {
            // El rol Cliente (usuario de un cliente del estudio) no se gestiona desde acá.
            if (Enum.TryParse<RolUsuario>(rol, ignoreCase: true, out var valor)
                && Enum.IsDefined(valor)
                && !int.TryParse(rol, out _)
                && valor != RolUsuario.Cliente)
                return valor;

            throw new ArgumentException($"Rol inválido: '{rol}'. Valores posibles: Administrador, Contador, Recepcion.");
        }
    }
}
