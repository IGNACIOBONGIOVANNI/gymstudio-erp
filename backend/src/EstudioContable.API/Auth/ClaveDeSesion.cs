using System.Security.Cryptography;

namespace EstudioContable.API.Auth
{
    /// <summary>
    /// De dónde sale la clave que firma las sesiones (y, si no tienen la suya, las copias de seguridad). Nunca del repositorio: una
    /// clave que viaja con el código le sirve a cualquiera que lo tenga para fabricarse una sesión de Administrador.
    /// Si está configurada (<c>Jwt:Key</c>, por ejemplo con la variable de entorno <c>Jwt__Key</c>) se usa esa. Si no, cada instalación
    /// genera la suya al azar la primera vez que arranca y la guarda en un archivo fuera del control de versiones, para que las
    /// sesiones sigan valiendo después de un reinicio.
    /// </summary>
    public static class ClaveDeSesion
    {
        public const string NombreDelArchivo = "jwt.key";
        public const int LargoMinimo = 32;

        /// <summary>Devuelve la clave y la deja en <c>Jwt:Key</c> de la configuración, de donde la leen los demás.</summary>
        public static string Resolver(IConfiguration configuracion, string carpeta)
        {
            var configurada = configuracion["Jwt:Key"];
            if (!string.IsNullOrWhiteSpace(configurada))
            {
                // Una clave puesta a mano y corta es un error de quien la puso: se avisa, no se la cambia por otra en silencio.
                if (configurada.Length < LargoMinimo)
                    throw new InvalidOperationException($"Jwt:Key tiene que tener al menos {LargoMinimo} caracteres.");
                return configurada;
            }

            var clave = LeerOCrear(Path.Combine(carpeta, NombreDelArchivo));
            configuracion["Jwt:Key"] = clave;
            return clave;
        }

        private static string LeerOCrear(string ruta)
        {
            if (!File.Exists(ruta))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
                var nueva = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
                // Se escribe aparte y se mueve sin pisar: si dos procesos arrancan a la vez, gana uno y el otro lee la de ese.
                var temporal = ruta + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(temporal, nueva);
                try { File.Move(temporal, ruta, overwrite: false); }
                catch (IOException) { File.Delete(temporal); }
            }

            var guardada = File.ReadAllText(ruta).Trim();
            if (guardada.Length < LargoMinimo)
                throw new InvalidOperationException($"El archivo de la clave de sesión ({ruta}) está dañado. Borralo para que se genere una clave nueva (habrá que volver a iniciar sesión).");
            return guardada;
        }
    }
}
