using System;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Gimnasio.Commands
{
    /// <summary>
    /// Lo que comparten las dos formas de cobrar una cuota para que dos pedidos a la vez (un doble clic, dos recepciones) no cobren
    /// dos veces: los cobros de un mismo socio pasan de a uno y un segundo cobro enseguida del primero se rechaza.
    /// </summary>
    public static class CobroDeCuotas
    {
        /// <summary>Cuánto hay que esperar para cobrarle otra cuota al mismo socio. Alcanza para frenar un doble clic o un reintento.</summary>
        public const int SegundosEntreCobros = 10;

        /// <summary>
        /// Hay que llamarlo dentro de la transacción del cobro y antes de leer al socio. Toma la fila del socio (los demás cobros de ese
        /// socio esperan hasta que esta transacción termine y recién ahí leen sus datos, ya actualizados) y rechaza el cobro si ya se le
        /// facturó una cuota hace instantes.
        /// </summary>
        /// <param name="momentoDelComprobante">La fecha que va a llevar la factura del cobro (<see cref="HoraArgentina.ParaComprobante"/>):
        /// las facturas de cuotas anteriores se comparan con ella, en la misma convención.</param>
        public static async Task TomarTurnoAsync(IAppDbContext context, long socioId, DateTime momentoDelComprobante, CancellationToken cancellationToken)
        {
            await context.BloquearSocioAsync(socioId, cancellationToken);

            var desde = momentoDelComprobante.AddSeconds(-SegundosEntreCobros);
            var reciente = await context.Facturas.AsNoTracking()
                .AnyAsync(f => f.SocioId == socioId && !f.Anulada && f.Fecha >= desde, cancellationToken);
            if (reciente)
                throw new InvalidOperationException(
                    "Ya se registró un cobro de cuota para este socio hace instantes. Revisá el Libro de Ventas; si querés cobrarle otro período, esperá unos segundos y volvé a intentarlo.");
        }
    }
}
