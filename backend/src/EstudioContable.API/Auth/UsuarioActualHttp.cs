using EstudioContable.Application.Interfaces;
using EstudioContable.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;

namespace EstudioContable.API.Auth
{
    /// <summary>Lee el usuario del JWT de la request actual y el equipo de su conexión y su User-Agent.</summary>
    public class UsuarioActualHttp : IUsuarioActual
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UsuarioActualHttp(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string? Email => _httpContextAccessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

        public string? Ip => AgenteDeUsuario.NormalizarIp(_httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString());

        public string? SistemaOperativo => Equipo?.SistemaOperativo;

        public string? Navegador => Equipo?.Navegador;

        private (string SistemaOperativo, string Navegador)? Equipo
        {
            get
            {
                var contexto = _httpContextAccessor.HttpContext;
                return contexto == null ? null : AgenteDeUsuario.Analizar(contexto.Request.Headers.UserAgent.ToString());
            }
        }
    }
}
