using System.Threading;
using System.Threading.Tasks;

namespace EstudioContable.Application.Interfaces
{
    /// <summary>
    /// Deja constancia en la auditoría de un hecho que no es un cambio de una tabla (un inicio de sesión, una copia de
    /// seguridad descargada o restaurada), con quién lo hizo y desde qué equipo (IP, sistema operativo y navegador).
    /// Nunca lanza una excepción: la auditoría no puede impedir la operación que registra.
    /// </summary>
    public interface IRegistroDeEventos
    {
        /// <param name="tabla">Tabla lógica del evento, por ejemplo "acceso" o "copia".</param>
        /// <param name="operacion">Hasta 10 caracteres, por ejemplo "INGRESO" o "RESTAURADA".</param>
        /// <param name="usuario">Quién lo hizo (el email, o el que se probó en un acceso fallido).</param>
        /// <param name="registroId">Sobre qué fila ocurrió, si el hecho es de una fila (por ejemplo, el id del entrenador al que se le cambió la foto). Hasta 50 caracteres.</param>
        Task RegistrarAsync(string tabla, string operacion, string usuario, CancellationToken cancellationToken, string? registroId = null);
    }
}
