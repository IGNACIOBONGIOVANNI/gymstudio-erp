using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EstudioContable.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        public class LoginRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            // MOCK TEMPORAL
            if (request.Email == "jefe@gym.com" && request.Password == "1234")
            {
                return Ok(new
                {
                    token = "mock-jwt-token-jefe-12345",
                    rol = "Jefe",
                    nombre = "Administrador Principal"
                });
            }

            if (request.Email == "empleado@gym.com" && request.Password == "1234")
            {
                return Ok(new
                {
                    token = "mock-jwt-token-empleado-67890",
                    rol = "Empleado",
                    nombre = "Recepcionista"
                });
            }

            return Unauthorized(new { message = "Credenciales incorrectas" });
        }
    }
}
