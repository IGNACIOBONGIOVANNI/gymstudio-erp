using System;

namespace EstudioContable.Domain
{
    /// <summary>
    /// El calendario semanal de encargados del gimnasio: de lunes a viernes, en turnos de una hora de 06:00 a 22:00.
    /// Estas reglas valen por cualquier camino (la API, la base las repite como restricciones) y deben coincidir con
    /// <c>frontend/src/features/calendario/calendarioUtil.ts</c>.
    /// </summary>
    public static class ReglasDeCalendario
    {
        /// <summary>Lunes.</summary>
        public const int PrimerDia = 1;
        /// <summary>Viernes.</summary>
        public const int UltimoDia = 5;
        /// <summary>El turno de 06:00 a 07:00 se guarda como 6.</summary>
        public const int PrimeraHora = 6;
        /// <summary>El turno de 21:00 a 22:00 se guarda como 21: el gimnasio cierra a las 22.</summary>
        public const int UltimaHora = 21;
        public const int LargoMaximoDelNombre = 80;
        /// <summary>Cantidad de celdas de la semana (5 días por 16 horas).</summary>
        public const int CeldasMaximas = (UltimoDia - PrimerDia + 1) * (UltimaHora - PrimeraHora + 1);

        public static void ValidarCelda(int dia, int hora)
        {
            if (dia < PrimerDia || dia > UltimoDia)
                throw new ArgumentException("El día tiene que ir de lunes (1) a viernes (5).");
            if (hora < PrimeraHora || hora > UltimaHora)
                throw new ArgumentException($"La hora tiene que ir de {PrimeraHora:00}:00 a {UltimaHora + 1:00}:00 (el último turno empieza a las {UltimaHora:00}).");
        }

        /// <summary>El nombre de quien está de encargado: sin espacios sobrantes, sin caracteres de control y con un largo razonable.</summary>
        public static string NombreDelEncargado(string? valor)
            => ReglasDeDatos.TextoRequerido(valor, "El nombre del encargado", LargoMaximoDelNombre);
    }
}
