using System;
using System.Collections.Generic;
using System.Linq;

namespace EstudioContable.Domain
{
    /// <summary>
    /// Política de contraseñas del sistema. Se aplica al crear un usuario o cambiarle la clave, en el servidor, así que vale por
    /// cualquier camino y no depende de la pantalla. No se aplica al iniciar sesión: una cuenta existente entra con la clave que tiene.
    /// </summary>
    /// <remarks>
    /// Exige: de 8 a 128 caracteres, una mayúscula, un número, un carácter especial y que ningún número vaya pegado a otro
    /// consecutivo (12, 45, 98: ascendente o descendente). Debe coincidir con <c>frontend/src/features/usuarios/reglasDeClave.ts</c>.
    /// </remarks>
    public static class ReglasDeClave
    {
        public const int LargoMinimo = 8;

        /// <summary>El mismo tope que el inicio de sesión: una clave más larga nunca podría usarse.</summary>
        public const int LargoMaximo = 128;

        /// <summary>Lo que le falta a la clave para cumplir la política, como requisitos ("una letra mayúscula"). Vacío si la cumple.</summary>
        public static IReadOnlyList<string> Problemas(string? clave)
        {
            var texto = clave ?? string.Empty;
            var problemas = new List<string>();
            if (texto.Length < LargoMinimo) problemas.Add($"al menos {LargoMinimo} caracteres");
            if (texto.Length > LargoMaximo) problemas.Add($"como máximo {LargoMaximo} caracteres");
            if (!texto.Any(char.IsUpper)) problemas.Add("una letra mayúscula");
            if (!texto.Any(EsNumero)) problemas.Add("un número");
            if (!texto.Any(EsEspecial)) problemas.Add("un carácter especial (por ejemplo ! ? # $ % & * - _ .)");
            if (TieneNumerosConsecutivos(texto)) problemas.Add("sin números consecutivos (como 12, 45 o 98)");
            if (texto.Any(char.IsControl)) problemas.Add("sin caracteres de control");
            return problemas;
        }

        /// <summary>Lanza <see cref="ArgumentException"/> con un mensaje claro si la clave no cumple la política.</summary>
        public static void Validar(string? clave)
        {
            var problemas = Problemas(clave);
            if (problemas.Count > 0)
                throw new ArgumentException("La contraseña no cumple la política de seguridad. Necesita: " + string.Join("; ", problemas) + ".");
        }

        private static bool EsNumero(char c) => c >= '0' && c <= '9';

        // Especial: ni letra, ni número, ni espacio, ni carácter de control.
        private static bool EsEspecial(char c) => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) && !char.IsControl(c);

        private static bool TieneNumerosConsecutivos(string texto)
        {
            for (var i = 1; i < texto.Length; i++)
                if (EsNumero(texto[i - 1]) && EsNumero(texto[i]) && Math.Abs(texto[i] - texto[i - 1]) == 1)
                    return true;
            return false;
        }
    }
}
