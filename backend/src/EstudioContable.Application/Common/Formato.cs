using System;
using System.Globalization;

namespace EstudioContable.Application.Common
{
    /// <summary>Formatos de texto para respuestas escritas (sin depender de la configuración regional del servidor).</summary>
    public static class Formato
    {
        /// <summary>Importe con el formato argentino: "$ 1.234.567,89" (y "-$ 1.234,00" si es negativo).</summary>
        public static string Pesos(decimal valor)
            => (valor < 0 ? "-" : "") + "$ " + Math.Abs(valor).ToString("N2", CultureInfo.InvariantCulture)
                .Replace(",", "#").Replace(".", ",").Replace("#", ".");
    }
}
