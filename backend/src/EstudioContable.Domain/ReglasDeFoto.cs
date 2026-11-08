using System;

namespace EstudioContable.Domain
{
    /// <summary>
    /// Qué foto de entrenador se acepta: un JPEG de 100 bytes a 150 000 bytes, completo (con su marca de fin) y de 16 a 512 píxeles de
    /// lado. Se mira la estructura del archivo, no solo la firma, y no se confía en la extensión ni en el tipo que declara quien lo manda.
    /// Un PNG, un SVG o un HTML disfrazado no pasan. La pantalla ya achica la foto a 256 × 256 antes de mandarla; esto es la defensa del servidor.
    /// </summary>
    public static class ReglasDeFoto
    {
        public const int BytesMinimos = 100;
        public const int BytesMaximos = 150_000;
        public const int LadoMinimo = 16;
        public const int LadoMaximo = 512;

        /// <summary>Lanza <see cref="ArgumentException"/> con un mensaje claro si el archivo no es una foto aceptable.</summary>
        public static void Validar(byte[]? foto)
        {
            if (foto is null || foto.Length == 0) throw new ArgumentException("La foto es requerida.");
            if (foto.Length < BytesMinimos) throw new ArgumentException("La foto es demasiado chica o está dañada.");
            if (foto.Length > BytesMaximos) throw new ArgumentException($"La foto no puede pesar más de {BytesMaximos / 1000} KB.");
            if (foto[0] != 0xFF || foto[1] != 0xD8 || foto[^2] != 0xFF || foto[^1] != 0xD9)
                throw new ArgumentException("La foto tiene que ser un archivo JPEG completo.");

            var tamano = TamanoDelJpeg(foto) ?? throw new ArgumentException("La foto tiene que ser un archivo JPEG válido.");
            var (ancho, alto) = tamano;
            if (ancho < LadoMinimo || alto < LadoMinimo || ancho > LadoMaximo || alto > LadoMaximo)
                throw new ArgumentException($"La foto tiene que medir entre {LadoMinimo} y {LadoMaximo} píxeles de lado.");
        }

        /// <summary>
        /// El ancho y el alto que declara la cabecera de tamaño (SOF) del JPEG, o null si no es un JPEG o no tiene esa cabecera antes de
        /// los datos de imagen. Recorre los segmentos sin decodificar la imagen.
        /// </summary>
        public static (int Ancho, int Alto)? TamanoDelJpeg(byte[] jpeg)
        {
            if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8) return null;

            var pos = 2;
            while (pos + 1 < jpeg.Length)
            {
                if (jpeg[pos] != 0xFF) return null;
                var marcador = jpeg[pos + 1];
                if (marcador == 0xFF) { pos++; continue; }   // bytes de relleno antes de un marcador

                // Marcadores sin largo: no hay nada que saltar. El fin de la imagen (D9) o un cero (que no es un marcador) cortan el recorrido.
                if (marcador == 0x01 || marcador is >= 0xD0 and <= 0xD8) { pos += 2; continue; }
                if (marcador == 0x00 || marcador == 0xD9 || marcador == 0xDA) return null;   // DA: empiezan los datos de imagen sin haber visto el tamaño

                if (pos + 4 > jpeg.Length) return null;
                var largo = (jpeg[pos + 2] << 8) | jpeg[pos + 3];
                if (largo < 2) return null;

                if (EsCabeceraDeTamano(marcador))
                {
                    // largo(2) + precisión(1) + alto(2) + ancho(2) + componentes(1) como mínimo
                    if (largo < 8 || pos + 2 + largo > jpeg.Length) return null;
                    var alto = (jpeg[pos + 5] << 8) | jpeg[pos + 6];
                    var ancho = (jpeg[pos + 7] << 8) | jpeg[pos + 8];
                    return (ancho, alto);
                }

                pos += 2 + largo;
            }
            return null;
        }

        // SOF0–SOF15 menos DHT (C4), JPG (C8) y DAC (CC), que comparten el rango pero no son una cabecera de tamaño.
        private static bool EsCabeceraDeTamano(byte marcador)
            => marcador is >= 0xC0 and <= 0xCF && marcador != 0xC4 && marcador != 0xC8 && marcador != 0xCC;
    }
}
