using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using EstudioContable.Application.Common;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain;
using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Enums;
using EstudioContable.Application.Notifications;
using MediatR;

namespace EstudioContable.Application.Ventas.Commands
{
    public class ImportarLibroVentaCommandHandler : IRequestHandler<ImportarLibroVentaCommand, ResultadoImportacion>
    {
        private readonly IAppDbContext _context;
        private readonly IMediator _mediator;

        public ImportarLibroVentaCommandHandler(IAppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<ResultadoImportacion> Handle(ImportarLibroVentaCommand request, CancellationToken cancellationToken)
        {
            // El archivo lo sube una persona: se controla que sea un Excel razonable antes de abrirlo.
            using var contenido = ArchivoExcelSeguro.Preparar(request.ArchivoExcel);
            XLWorkbook workbook;
            try { workbook = new XLWorkbook(contenido); }
            catch (Exception) { throw new ArgumentException("El archivo no es un Excel (.xlsx) válido."); }

            using var _ = workbook;
            var worksheet = workbook.Worksheets.Worksheet(1); // Tomar la primera hoja
            var rowCount = worksheet.LastRowUsed()?.RowNumber() ?? 0;
            if (rowCount > ArchivoExcelSeguro.FilasMaximas)
                throw new ArgumentException($"El archivo tiene más de {ArchivoExcelSeguro.FilasMaximas:N0} filas.");

            var facturasParaInsertar = new List<FacturaVenta>();
            var omitidas = 0;

            // Asumimos que la fila 1 son los encabezados
            for (int i = 2; i <= rowCount; i++)
            {
                var row = worksheet.Row(i);
                if (row.IsEmpty()) continue;

                var fechaStr = row.Cell(1).GetString();
                if (!DateTime.TryParse(fechaStr, out DateTime fecha))
                {
                    omitidas++;
                    continue; // Saltar filas con formato de fecha inválido
                }

                var tipoFactura = row.Cell(2).GetString();
                var numeroFactura = row.Cell(3).GetString();
                var nombre = row.Cell(4).GetString();
                var cuit = row.Cell(5).GetString();
                
                if (!decimal.TryParse(row.Cell(6).GetString(), out decimal neto)) neto = 0;
                if (!decimal.TryParse(row.Cell(7).GetString(), out decimal iva)) iva = 0;
                if (!decimal.TryParse(row.Cell(8).GetString(), out decimal total)) total = neto + iva;

                // Leer condición de pago (columna 9)
                var condicionPagoStr = row.Cell(9).GetString();
                CondicionPago condicionPago = CondicionPago.CuentaCorriente;
                // IsDefined: Enum.TryParse también acepta números sueltos ("99") que no son una condición de pago.
                if (Enum.TryParse<CondicionPago>(condicionPagoStr, true, out var parsedCondicion) && Enum.IsDefined(parsedCondicion))
                {
                    condicionPago = parsedCondicion;
                }

                // Una fila con datos que no tienen forma de factura (tipo, número, documento, importes) se omite.
                var error = ReglasDeComprobante.ValidarCabecera(fecha, tipoFactura, numeroFactura, nombre, cuit)
                            ?? ReglasDeComprobante.ValidarImportes(neto, iva, total);
                if (error != null)
                {
                    omitidas++;
                    continue;
                }

                var factura = new FacturaVenta(
                    request.ClienteId.GetValueOrDefault(1),
                    fecha,
                    tipoFactura,
                    numeroFactura,
                    nombre,
                    cuit,
                    neto,
                    iva,
                    total,
                    condicionPago
                );

                facturasParaInsertar.Add(factura);
            }

            if (facturasParaInsertar.Count > 0)
            {
                _context.FacturasVenta.AddRange(facturasParaInsertar);
                await _context.SaveChangesAsync(cancellationToken);

                // Publicar notificaciones
                foreach (var factura in facturasParaInsertar)
                {
                    await _mediator.Publish(new FacturaVentaCreadaNotification(factura), cancellationToken);
                }
            }

            return new ResultadoImportacion(facturasParaInsertar.Count, omitidas);
        }
    }
}
