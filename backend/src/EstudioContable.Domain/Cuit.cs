using System.Linq;

namespace EstudioContable.Domain
{
    /// <summary>
    /// Validación de CUIT/CUIL argentino: 11 dígitos con dígito verificador (módulo 11).
    /// </summary>
    public static class Cuit
    {
        private static readonly int[] Multiplicadores = { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };

        /// <summary>Deja sólo los dígitos: "20-11111111-2" -> "20111111112".</summary>
        public static string Normalizar(string? cuit) => new string((cuit ?? string.Empty).Where(char.IsDigit).ToArray());

        public static bool EsValido(string? cuit)
        {
            var digitos = Normalizar(cuit);
            if (digitos.Length != 11)
                return false;

            var suma = Multiplicadores.Select((m, i) => m * (digitos[i] - '0')).Sum();
            var verificador = 11 - (suma % 11);
            if (verificador == 11) verificador = 0;
            if (verificador == 10) return false;

            return verificador == digitos[10] - '0';
        }

        /// <summary>"20111111112" -> "20-11111111-2"</summary>
        public static string Formatear(string cuitNormalizado)
            => cuitNormalizado.Length == 11 ? $"{cuitNormalizado[..2]}-{cuitNormalizado[2..10]}-{cuitNormalizado[10]}" : cuitNormalizado;
    }
}
