using System;
using System.IO;
using System.IO.Compression;

namespace EstudioContable.Application.Common
{
    /// <summary>
    /// Controles previos a abrir un Excel (.xlsx) que sube una persona. Un .xlsx es un zip: sin estos topes,
    /// un archivo chico que al descomprimirse ocupa gigas ("bomba zip"), uno que no es un Excel o una hoja con
    /// millones de filas gastarían toda la memoria del servidor.
    /// </summary>
    public static class ArchivoExcelSeguro
    {
        public const long BytesMaximos = 5L * 1024 * 1024;
        public const long BytesDescomprimidosMaximos = 50L * 1024 * 1024;
        public const int EntradasMaximas = 200;
        public const int FilasMaximas = 10_000;

        private static readonly byte[] FirmaZip = { 0x50, 0x4B, 0x03, 0x04 };

        /// <summary>
        /// Copia el archivo a memoria (con tope) y comprueba que sea un zip razonable. Devuelve el contenido listo
        /// para leer desde el principio. Lanza ArgumentException con un mensaje claro si no pasa los controles.
        /// </summary>
        public static MemoryStream Preparar(Stream origen)
        {
            var memoria = new MemoryStream();
            var buffer = new byte[81920];
            int leidos;
            while ((leidos = origen.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (memoria.Length + leidos > BytesMaximos)
                    throw new ArgumentException($"El archivo supera los {BytesMaximos / (1024 * 1024)} MB permitidos.");
                memoria.Write(buffer, 0, leidos);
            }

            var contenido = memoria.GetBuffer();
            if (memoria.Length < FirmaZip.Length || !contenido.AsSpan(0, FirmaZip.Length).SequenceEqual(FirmaZip))
                throw new ArgumentException("El archivo no es un Excel (.xlsx) válido.");

            memoria.Position = 0;
            VerificarQueNoSeaUnaBombaZip(memoria);
            memoria.Position = 0;
            return memoria;
        }

        /// <summary>
        /// Descomprime de verdad (contando lo que sale, no lo que el archivo declara, que puede mentir) hasta un tope.
        /// </summary>
        private static void VerificarQueNoSeaUnaBombaZip(MemoryStream memoria)
        {
            try
            {
                using var zip = new ZipArchive(memoria, ZipArchiveMode.Read, leaveOpen: true);
                if (zip.Entries.Count > EntradasMaximas)
                    throw new ArgumentException("El archivo no es un Excel (.xlsx) válido.");

                long total = 0;
                var buffer = new byte[81920];
                foreach (var entrada in zip.Entries)
                {
                    using var datos = entrada.Open();
                    int leidos;
                    while ((leidos = datos.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        total += leidos;
                        if (total > BytesDescomprimidosMaximos)
                            throw new ArgumentException("El archivo es demasiado grande para importarlo.");
                    }
                }
            }
            catch (InvalidDataException)
            {
                throw new ArgumentException("El archivo no es un Excel (.xlsx) válido.");
            }
        }
    }
}
