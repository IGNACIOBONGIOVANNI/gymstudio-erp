using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using EstudioContable.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace EstudioContable.API.Auth
{
    public class TokenService
    {
        private readonly JwtOptions _options;

        public TokenService(IOptions<JwtOptions> options)
        {
            _options = options.Value;
        }

        public static SymmetricSecurityKey CrearClave(string key) => new(Encoding.UTF8.GetBytes(key));

        public (string Token, DateTime Expira) Generar(Usuario usuario)
        {
            var expira = DateTime.UtcNow.AddMinutes(_options.ExpiracionMinutos);

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                Expires = expira,
                SigningCredentials = new SigningCredentials(CrearClave(_options.Key), SecurityAlgorithms.HmacSha256),
                Claims = new Dictionary<string, object>
                {
                    [JwtRegisteredClaimNames.Sub] = usuario.Id.ToString(),
                    [JwtRegisteredClaimNames.Email] = usuario.Email,
                    [JwtRegisteredClaimNames.Name] = usuario.NombreCompleto,
                    ["role"] = usuario.Rol.ToString(),
                    [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString()
                }
            };

            return (new JsonWebTokenHandler().CreateToken(descriptor), expira);
        }

        /// <summary>Id del usuario autenticado (claim "sub" del token).</summary>
        public static long ObtenerUsuarioId(ClaimsPrincipal user)
        {
            var sub = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (!long.TryParse(sub, out var id))
                throw new UnauthorizedAccessException("El token no contiene un identificador de usuario válido.");
            return id;
        }
    }
}
