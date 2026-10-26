using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Domain.Entities;

namespace EstudioContable.Domain.Interfaces
{
    /// <summary>
    /// Contrato de persistencia del agregado AsientoContable. Vive en Domain
    /// (no en Infrastructure) para que Application dependa sólo de abstracciones,
    /// siguiendo la Regla de Dependencia de Clean Architecture.
    /// </summary>
    public interface IAsientoRepository
    {
        /// <summary>
        /// Obtiene un asiento junto con todas sus líneas (Include obligatorio:
        /// AsientoContable.Confirmar() necesita las líneas cargadas para calcular
        /// TotalDebe/TotalHaber). Devuelve null si no existe.
        /// </summary>
        Task<AsientoContable?> ObtenerPorIdConLineasAsync(long asientoId, CancellationToken cancellationToken);

        Task<System.Collections.Generic.List<AsientoContable>> ObtenerTodosAsync(long? clienteId, CancellationToken cancellationToken);

        void Agregar(AsientoContable asiento);

        void Eliminar(AsientoContable asiento);

        Task<long> ObtenerMaximoNumeroAsientoAsync(long clienteId, CancellationToken cancellationToken);
    }
}
