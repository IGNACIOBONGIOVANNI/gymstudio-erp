using EstudioContable.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;

namespace EstudioContable.API.Auth
{
    /// <summary>Lee el email del usuario desde el JWT de la request actual.</summary>
    public class UsuarioActualHttp : IUsuarioActual
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UsuarioActualHttp(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string? Email => _httpContextAccessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
    }
}
