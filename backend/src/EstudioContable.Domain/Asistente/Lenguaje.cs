using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace EstudioContable.Domain.Asistente
{
    /// <summary>Período al que se refiere una pregunta: de <see cref="Desde"/> (incluido) a <see cref="Hasta"/> (excluido).</summary>
    public record Periodo(DateOnly Desde, DateOnly Hasta, string Etiqueta);

    /// <summary>
    /// Herramientas para entender preguntas escritas en lenguaje natural, sin servicios externos:
    /// normalizar el texto (sin tildes ni mayúsculas ni signos), buscar palabras por su raíz y reconocer períodos.
    /// </summary>
    public static class Lenguaje
    {
        private static readonly string[] Meses =
            { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };

        public static string NombreDelMes(int mes) => Meses[mes - 1];

        /// <summary>"¿Cuánto VENDÍ en Octubre?" -> "cuanto vendi en octubre".</summary>
        public static string Normalizar(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
            var descompuesto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(descompuesto.Length);
            foreach (var c in descompuesto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;   // quita las tildes
                sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
            }
            return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>
        /// Verdadero si alguna palabra del texto empieza con alguna de las raíces ("vend" encuentra vendí, vendimos y vendido).
        /// El texto debe venir ya normalizado.
        /// </summary>
        public static bool Tiene(string textoNormalizado, params string[] raices)
        {
            var palabras = textoNormalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return palabras.Any(p => raices.Any(r => p.StartsWith(r, StringComparison.Ordinal)));
        }

        /// <summary>Verdadero si el texto contiene la frase empezando en el comienzo de una palabra ("mas vend" encuentra "el mas vendido").</summary>
        public static bool TieneFrase(string textoNormalizado, params string[] frases)
            => frases.Any(f => (" " + textoNormalizado).Contains(" " + f, StringComparison.Ordinal));

        /// <summary>
        /// Período de la pregunta: hoy, este año, el mes pasado, un mes con nombre (con año opcional) o, si no dice nada,
        /// el mes actual. El texto debe venir normalizado.
        /// </summary>
        public static Periodo InterpretarPeriodo(string textoNormalizado, DateOnly hoy)
        {
            var primero = new DateOnly(hoy.Year, hoy.Month, 1);

            if (TieneFrase(textoNormalizado, "hoy"))
                return new Periodo(hoy, hoy.AddDays(1), "hoy");

            if (TieneFrase(textoNormalizado, "este ano", "en el ano", "del ano", "anual", "en lo que va del ano"))
                return new Periodo(new DateOnly(hoy.Year, 1, 1), new DateOnly(hoy.Year + 1, 1, 1), $"este año ({hoy.Year})");

            if (TieneFrase(textoNormalizado, "mes pasado", "mes anterior", "ultimo mes", "el mes que paso"))
            {
                var anterior = primero.AddMonths(-1);
                return DelMes(anterior);
            }

            var palabras = textoNormalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < palabras.Length; i++)
            {
                var mes = Array.IndexOf(Meses, palabras[i] == "setiembre" ? "septiembre" : palabras[i]) + 1;
                if (mes == 0) continue;

                // Año explícito: "marzo de 2025" o "marzo 2025".
                var anio = hoy.Year;
                foreach (var candidata in palabras.Skip(i + 1).Take(2))
                    if (int.TryParse(candidata, NumberStyles.None, CultureInfo.InvariantCulture, out var a) && a is >= 2000 and <= 2100)
                    { anio = a; break; }
                return DelMes(new DateOnly(anio, mes, 1));
            }

            return new Periodo(primero, primero.AddMonths(1), $"este mes ({NombreDelMes(primero.Month)} de {primero.Year})");
        }

        private static Periodo DelMes(DateOnly primerDia)
            => new(primerDia, primerDia.AddMonths(1), $"{NombreDelMes(primerDia.Month)} de {primerDia.Year}");
    }
}
