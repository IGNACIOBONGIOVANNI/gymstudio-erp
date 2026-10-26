namespace EstudioContable.API.Auth
{
    /// <summary>Sección "Jwt" de appsettings.json.</summary>
    public class JwtOptions
    {
        public const string Seccion = "Jwt";

        public string Issuer { get; set; } = "EstudioContable.API";
        public string Audience { get; set; } = "EstudioContable.Frontend";

        // Clave HMAC de al menos 32 caracteres. En producción se configura por
        // variable de entorno (Jwt__Key), nunca en el repositorio.
        public string Key { get; set; } = string.Empty;

        // Duración de la sesión: un turno de trabajo.
        public int ExpiracionMinutos { get; set; } = 480;
    }
}
