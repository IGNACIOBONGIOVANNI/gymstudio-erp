using System;
using System.Text.RegularExpressions;

namespace EstudioContable.Domain
{
    /// <summary>
    /// Cómo se escriben el nombre y el teléfono de una persona (entrenador, socio, usuario). Valen por cualquier camino
    /// (la API, una importación) y no dependen de que la pantalla los controle. Los nombres de empresa (razón social de
    /// clientes y proveedores) no pasan por acá: llevan números y puntos y siguen siendo texto libre.
    /// </summary>
    public static class ReglasDePersonas
    {
        /// <summary>Un teléfono internacional tiene a lo sumo 15 dígitos entre el código del país y el número.</summary>
        public const int DigitosMaximosDelTelefono = 15;

        // Cualquier racha de espacios (el duro que trae pegar desde WhatsApp o Word, el ideográfico, el separador de línea...) pasa
        // a un solo espacio común. El tabulador y el salto de línea no están acá: son caracteres de control y se rechazan.
        private static readonly Regex Espacios = new(@"[\p{Zs}\p{Zl}\p{Zp}]+", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        // Empieza con una letra y sigue con letras (de cualquier alfabeto), marcas combinadas (la tilde suelta), espacios, puntos
        // (Lic., Dr.), apóstrofos (D'Angelo, también el curvo) y guiones (Pérez-Gómez). Ningún dígito, de ningún alfabeto.
        private static readonly Regex FormaDelNombre = new(@"^\p{L}[\p{L}\p{M} '’.\-]*\z", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        // +código (1 a 3 dígitos), un espacio y el número (4 a 14 dígitos): solo 0-9, no los dígitos de otros alfabetos.
        private static readonly Regex FormaDelTelefono = new(@"^\+([0-9]{1,3}) ([0-9]{4,14})\z", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        /// <summary>
        /// El nombre de una persona como se guarda: obligatorio, sin espacios sobrantes (los raros pasan a espacios comunes), de hasta
        /// <paramref name="maximo"/> caracteres y solo con letras, espacios, puntos, apóstrofos y guiones. Las mayúsculas y los acentos se conservan.
        /// </summary>
        public static string NombreDePersona(string? valor, string campo, int maximo)
        {
            var texto = ReglasDeDatos.TextoRequerido(Espacios.Replace(valor ?? string.Empty, " "), campo, maximo);
            if (!FormaDelNombre.IsMatch(texto))
                throw new ArgumentException($"{campo} solo puede tener letras, espacios, puntos, apóstrofos y guiones.");
            return texto;
        }

        /// <summary>
        /// El teléfono (o WhatsApp) como se guarda: opcional (vacío es null) y, si se carga, con el código del país y solo números:
        /// <c>+54 1155551234</c>. El número no lleva espacios ni guiones, y entre el código y el número suman hasta 15 dígitos.
        /// </summary>
        public static string? Telefono(string? valor, string campo)
        {
            var texto = Espacios.Replace(valor ?? string.Empty, " ").Trim();
            if (texto.Length == 0) return null;

            var forma = FormaDelTelefono.Match(texto);
            if (!forma.Success || forma.Groups[1].Length + forma.Groups[2].Length > DigitosMaximosDelTelefono)
                throw new ArgumentException($"{campo} tiene que llevar el código del país y solo números, por ejemplo +54 1155551234.");
            return texto;
        }
    }
}
