using System.IO;
using MediatR;

namespace EstudioContable.Application.Ventas.Commands
{
    public record ImportarLibroVentaCommand(long? ClienteId, Stream ArchivoExcel) : IRequest<int>;
}
