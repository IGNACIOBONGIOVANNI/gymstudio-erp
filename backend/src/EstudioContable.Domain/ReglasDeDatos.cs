using System;
using System.Linq;

namespace EstudioContable.Domain
{
    /// <summary>
    /// Topes y formatos de los datos que cargan las personas. Se aplican en el dominio, así valen por cualquier
    /// camino (la API, una importación) y no dependen de que la pantalla los controle.
    /// Se valida lo que debe tener forma (números, códigos); el texto libre se acepta tal cual, siempre que
    /// tenga largo razonable y ningún carácter de control, porque se guarda con parámetros y se muestra escapado.
    /// </summary>
    public static class ReglasDeDatos
    {
        /// <summary>Importe máximo de un precio o de una factura: queda por debajo del límite de numeric(18,2).</summary>
        public const decimal ImporteMaximo = 999_999_999_999m;

        public const int MesesMaximosDeUnPlan = 120;
        public const int StockMaximo = 1_000_000;
        public const decimal NutricionMaxima = 100_000m;

        /// <summary>Texto obligatorio: sin espacios sobrantes, sin caracteres de control y de hasta <paramref name="maximo"/> caracteres.</summary>
        public static string TextoRequerido(string? valor, string campo, int maximo)
        {
            var texto = (valor ?? string.Empty).Trim();
            if (texto.Length == 0) throw new ArgumentException($"{campo} es requerido.");
            return ValidarTexto(texto, campo, maximo);
        }

        /// <summary>Texto opcional: vacío se guarda como null.</summary>
        public static string? TextoOpcional(string? valor, string campo, int maximo)
        {
            var texto = (valor ?? string.Empty).Trim();
            return texto.Length == 0 ? null : ValidarTexto(texto, campo, maximo);
        }

        private static string ValidarTexto(string texto, string campo, int maximo)
        {
            if (texto.Length > maximo) throw new ArgumentException($"{campo} no puede tener más de {maximo} caracteres.");
            if (texto.Any(char.IsControl)) throw new ArgumentException($"{campo} tiene caracteres no permitidos.");
            return texto;
        }

        /// <summary>Importe entre 0 y el máximo.</summary>
        public static decimal Importe(decimal valor, string campo)
        {
            if (valor < 0) throw new ArgumentException($"{campo} no puede ser negativo.");
            if (valor > ImporteMaximo) throw new ArgumentException($"{campo} es demasiado grande.");
            return valor;
        }

        public static int Entero(int valor, string campo, int minimo, int maximo)
        {
            if (valor < minimo || valor > maximo) throw new ArgumentException($"{campo} tiene que estar entre {minimo} y {maximo}.");
            return valor;
        }

        /// <summary>Dato nutricional opcional: de 0 en adelante y con tope.</summary>
        public static decimal? Nutricion(decimal? valor, string campo)
        {
            if (valor == null) return null;
            if (valor < 0 || valor > NutricionMaxima) throw new ArgumentException($"{campo} tiene que estar entre 0 y {NutricionMaxima:0}.");
            return valor;
        }

        /// <summary>Código de cuenta contable: números separados por puntos ("1.1.1.01"), de hasta 20 caracteres.</summary>
        public static bool EsCodigoDeCuenta(string? codigo)
        {
            if (string.IsNullOrEmpty(codigo) || codigo.Length > 20) return false;
            var partes = codigo.Split('.');
            return partes.Length <= 7 && partes.All(p => p.Length is >= 1 and <= 4 && p.All(char.IsAsciiDigit));
        }
    }
}
