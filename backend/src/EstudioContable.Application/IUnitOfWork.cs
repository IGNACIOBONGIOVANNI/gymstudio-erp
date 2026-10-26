using System.Threading;
using System.Threading.Tasks;

namespace EstudioContable.Application.Common.Interfaces
{
    /// <summary>
    /// Abstrae la unidad de trabajo (transacción) sobre la que operan los handlers.
    /// La implementación concreta (Infrastructure) envuelve AppDbContext.SaveChangesAsync,
    /// que ya incluye la revalidación de balance como red de seguridad final.
    /// </summary>
    public interface IUnitOfWork
    {
        /// <summary>
        /// Persiste todos los cambios trackeados dentro de una única transacción implícita.
        /// Devuelve la cantidad de filas afectadas.
        /// </summary>
        Task<int> GuardarCambiosAsync(CancellationToken cancellationToken);
    }
}
