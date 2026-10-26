using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Domain.Entities;

namespace EstudioContable.Application.Interfaces
{
    /// <summary>
    /// Único punto del sistema que genera y revierte los efectos de una Factura:
    /// asiento contable, movimiento de caja/banco y movimiento de stock.
    /// </summary>
    public interface IFacturacionService
    {
        Task<Factura> CrearFacturaAsync(Factura factura, CancellationToken cancellationToken);

        /// <summary>
        /// Marca la factura como anulada y revierte sus efectos con contraasiento,
        /// movimiento de tesorería inverso y devolución de stock. No borra nada.
        /// </summary>
        Task AnularFacturaAsync(long facturaId, CancellationToken cancellationToken);

        /// <summary>
        /// Reemplaza los datos de una factura: revierte los efectos de la versión
        /// anterior y aplica los de la nueva, dejando la traza en los libros.
        /// </summary>
        Task ReemplazarFacturaAsync(long facturaId, Factura datosNuevos, CancellationToken cancellationToken);
    }
}
