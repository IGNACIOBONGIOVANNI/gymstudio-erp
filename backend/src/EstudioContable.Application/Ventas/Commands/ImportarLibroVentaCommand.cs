using System.IO;
using MediatR;

namespace EstudioContable.Application.Ventas.Commands
{
    public record ImportarLibroVentaCommand(long? ClienteId, Stream ArchivoExcel) : IRequest<ResultadoImportacion>;

    /// <summary>Cuántas filas se importaron y cuántas se omitieron por tener datos inválidos.</summary>
    public record ResultadoImportacion(int Importadas, int Omitidas);
}
