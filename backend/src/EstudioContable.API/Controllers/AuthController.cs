using System.Threading;
using System.Threading.Tasks;
using EstudioContable.API.Auth;
using EstudioContable.Domain.Entities;
using EstudioContable.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        public const string PoliticaLimiteLogin = "login";

        private readonly AppDbContext _context;
        private readonly IPasswordHasher<Usuario> _hasher;
        private readonly TokenService _tokenService;

        public AuthController(AppDbContext context, IPasswordHasher<Usuario> hasher, TokenService tokenService)
        {
            _context = context;
            _hasher = hasher;
            _tokenService = tokenService;
        }

        public class LoginRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting(PoliticaLimiteLogin)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
        {
            var email = Usuario.NormalizarEmail(request.Email);
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

            // Mismo mensaje para usuario inexistente y contraseña incorrecta:
            // no revela qué emails están registrados.
            var resultado = usuario == null
                ? PasswordVerificationResult.Failed
                : _hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, request.Password ?? string.Empty);

            if (usuario == null || resultado == PasswordVerificationResult.Failed)
                return Unauthorized(new { detail = "Credenciales incorrectas" });

            if (!usuario.Activo)
                return Unauthorized(new { detail = "Esta cuenta se encuentra inactiva. Contacte al administrador." });

            // Si el algoritmo de hash se actualizó, se re-hashea con el actual.
            if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
                usuario.CambiarPassword(_hasher.HashPassword(usuario, request.Password!));

            usuario.RegistrarLogin();
            await _context.SaveChangesAsync(cancellationToken);

            var (token, expira) = _tokenService.Generar(usuario);

            return Ok(new
            {
                token,
                expira,
                rol = usuario.Rol.ToString(),
                nombre = usuario.NombreCompleto,
                email = usuario.Email
            });
        }

        /// <summary>Datos del usuario dueño del token (sirve para validar la sesión).</summary>
        [HttpGet("me")]
        public async Task<IActionResult> Me(CancellationToken cancellationToken)
        {
            var id = TokenService.ObtenerUsuarioId(User);
            var usuario = await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
            if (usuario == null || !usuario.Activo)
                return Unauthorized(new { detail = "La sesión ya no es válida." });

            return Ok(new { id = usuario.Id, nombre = usuario.NombreCompleto, email = usuario.Email, rol = usuario.Rol.ToString() });
        }
    }
}
