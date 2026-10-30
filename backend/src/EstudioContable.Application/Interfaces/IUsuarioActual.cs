namespace EstudioContable.Application.Interfaces
{
    /// <summary>
    /// Quién hace la request actual y desde qué equipo (null fuera de una request, ej. al arrancar).
    /// El usuario sale del token; la IP, el sistema operativo y el navegador, de la conexión y del
    /// encabezado User-Agent, y sirven para la auditoría.
    /// </summary>
    public interface IUsuarioActual
    {
        string? Email { get; }

        /// <summary>IP del equipo, ya corregida si pasó por un proxy de confianza (X-Forwarded-For).</summary>
        string? Ip { get; }

        string? SistemaOperativo { get; }
        string? Navegador { get; }
    }
}
