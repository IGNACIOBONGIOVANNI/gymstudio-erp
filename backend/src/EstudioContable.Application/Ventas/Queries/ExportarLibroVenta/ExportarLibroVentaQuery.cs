using MediatR;

namespace EstudioContable.Application.Ventas.Queries.ExportarLibroVenta
{
    public record ExportarLibroVentaQuery(long? ClienteId) : IRequest<byte[]>;
}
