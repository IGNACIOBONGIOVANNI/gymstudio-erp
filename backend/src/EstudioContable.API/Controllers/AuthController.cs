using System.Threading;
using System.Threading.Tasks;
using EstudioContable.API.Auth;
using EstudioContable.Application.Interfaces;
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
        private readonly IRegistroDeEventos _eventos;

        public AuthController(AppDbContext context, IPasswordHasher<Usuario> hasher, TokenService tokenService, IRegistroDeEventos eventos)
        {
            _context = context;
            _hasher = hasher;
            _tokenService = tokenService;
            _eventos = eventos;
        }

        public class LoginRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        private const int FallosParaBloquear = 5;
        private static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(15);
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int Fallos, DateTime Desde)> _fallos = new();

        // Topes para que nadie use el login para gastar CPU o memoria del servidor.
        private const int LargoMaximoEmail = 254;
        private const int LargoMaximoPassword = 128;
        private const int MaximoDeCuentasRecordadas = 5_000;

        // Hash de una contraseña que nadie conoce: cuando el email no existe se calcula igual, así la
        // respuesta tarda lo mismo y no se puede descubrir qué emails están registrados midiendo el tiempo.
        private static readonly PasswordHasher<Usuario> _hasherFalso = new();
        private static readonly string _hashFalso = _hasherFalso.HashPassword(null!, Guid.NewGuid().ToString("N"));

        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting(PoliticaLimiteLogin)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
        {
            var email = Usuario.NormalizarEmail(request.Email);
            var password = request.Password ?? string.Empty;

            // Entradas absurdas: se rechazan sin tocar la base, sin calcular el hash y sin recordarlas.
            if (email.Length == 0 || email.Length > LargoMaximoEmail || password.Length > LargoMaximoPassword
                || email.Any(char.IsControl))
            {
                await RegistrarAccesoAsync("FALLIDO", email, cancellationToken);
                return Unauthorized(new { detail = "Credenciales incorrectas" });
            }

            if (_fallos.TryGetValue(email, out var estado))
            {
                if (DateTime.UtcNow - estado.Desde > DuracionBloqueo)
                    _fallos.TryRemove(email, out _);
                else if (estado.Fallos >= FallosParaBloquear)
                {
                    await RegistrarAccesoAsync("BLOQUEADO", email, cancellationToken);
                    return StatusCode(StatusCodes.Status429TooManyRequests,
                        new { detail = "Demasiados intentos fallidos con esta cuenta. Esperá 15 minutos o pedile al administrador que revise el acceso." });
                }
            }

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

            // Mismo mensaje y mismo tiempo para usuario inexistente y contraseña incorrecta:
            // no revela qué emails están registrados.
            PasswordVerificationResult resultado;
            if (usuario == null)
            {
                _hasherFalso.VerifyHashedPassword(null!, _hashFalso, password);
                resultado = PasswordVerificationResult.Failed;
            }
            else
            {
                resultado = _hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, password);
            }

            if (usuario == null || resultado == PasswordVerificationResult.Failed)
            {
                RecordarFallo(email);
                await RegistrarAccesoAsync("FALLIDO", email, cancellationToken);
                return Unauthorized(new { detail = "Credenciales incorrectas" });
            }
            _fallos.TryRemove(email, out _);

            if (!usuario.Activo)
            {
                await RegistrarAccesoAsync("FALLIDO", email, cancellationToken);
                return Unauthorized(new { detail = "Esta cuenta se encuentra inactiva. Contacte al administrador." });
            }

            // Si el algoritmo de hash se actualizó, se re-hashea con el actual.
            if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
                usuario.CambiarPassword(_hasher.HashPassword(usuario, password));

            usuario.RegistrarLogin();
            await _context.SaveChangesAsync(cancellationToken);
            await RegistrarAccesoAsync("INGRESO", usuario.Email, cancellationToken);

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

        /// <summary>
        /// Suma un fallo a la cuenta. Si alguien prueba miles de emails distintos no se acumulan sin límite:
        /// se descartan los vencidos y, si aun así no hay lugar, no se recuerda un email nuevo (las cuentas
        /// que ya estaban bloqueadas siguen bloqueadas).
        /// </summary>
        private static void RecordarFallo(string email)
        {
            if (!_fallos.ContainsKey(email) && _fallos.Count >= MaximoDeCuentasRecordadas)
            {
                var limite = DateTime.UtcNow - DuracionBloqueo;
                foreach (var par in _fallos)
                    if (par.Value.Desde < limite) _fallos.TryRemove(par.Key, out _);
                if (_fallos.Count >= MaximoDeCuentasRecordadas) return;
            }
            _fallos.AddOrUpdate(email, _ => (1, DateTime.UtcNow), (_, e) => (e.Fallos + 1, e.Fallos == 0 ? DateTime.UtcNow : e.Desde));
        }

        /// <summary>
        /// Deja constancia en la auditoría de cada inicio de sesión: INGRESO, FALLIDO o BLOQUEADO, con el email que se probó y
        /// el equipo (IP, sistema operativo y navegador). Nunca se guarda la contraseña. Si el registro falla, el login sigue.
        /// </summary>
        private Task RegistrarAccesoAsync(string operacion, string email, CancellationToken cancellationToken)
            => _eventos.RegistrarAsync("acceso", operacion, email, cancellationToken);

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
