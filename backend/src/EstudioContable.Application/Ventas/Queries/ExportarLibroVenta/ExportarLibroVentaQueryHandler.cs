using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using EstudioContable.Application.Ventas.Queries.ObtenerLibroVenta;
using MediatR;

namespace EstudioContable.Application.Ventas.Queries.ExportarLibroVenta
{
    public class ExportarLibroVentaQueryHandler : IRequestHandler<ExportarLibroVentaQuery, byte[]>
    {
        private readonly IMediator _mediator;

        public ExportarLibroVentaQueryHandler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<byte[]> Handle(ExportarLibroVentaQuery request, CancellationToken cancellationToken)
        {
            // Reutilizamos la consulta existente para obtener la data del cliente
            var ventas = await _mediator.Send(new ObtenerLibroVentaQuery(request.ClienteId), cancellationToken);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Libro Venta");

            // Cabeceras
            var cabeceras = new[] { "Fecha", "Tipo de Fact.", "N° de Fact", "Nombre", "CUIT", "Neto", "IVA", "Total", "Condición de Pago" };
            for (int i = 0; i < cabeceras.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = cabeceras[i];
                worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            }

            // Datos
            int fila = 2;
            foreach (var venta in ventas)
            {
                worksheet.Cell(fila, 1).Value = venta.Fecha.ToString("dd/MM/yyyy");
                worksheet.Cell(fila, 2).Value = venta.TipoFactura;
                worksheet.Cell(fila, 3).Value = venta.NumeroFactura;
                worksheet.Cell(fila, 4).Value = venta.ClienteNombre;
                worksheet.Cell(fila, 5).Value = venta.Cuit;
                worksheet.Cell(fila, 6).Value = venta.Neto;
                worksheet.Cell(fila, 7).Value = venta.Iva;
                worksheet.Cell(fila, 8).Value = venta.Total;
                worksheet.Cell(fila, 9).Value = venta.CondicionPago;
                fila++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}
