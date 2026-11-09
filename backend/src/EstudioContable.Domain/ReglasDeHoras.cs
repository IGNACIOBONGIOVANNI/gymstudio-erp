using System;
using System.Collections.Generic;
using System.Linq;

namespace EstudioContable.Domain
{
    /// <summary>
    /// Reglas puras de las horas cumplidas de los entrenadores y de su presupuesto de pago: qué horas le tocaban a cada uno según el calendario,
    /// qué horas, valores, fechas y períodos se aceptan y cómo se calcula el monto. Sin base de datos ni reloj propio: el «hoy» entra como dato,
    /// y es el de Argentina (UTC-3), como el resto del sistema. Todo el dinero va con <see cref="decimal"/>.
    /// </summary>
    public static class ReglasDeHoras
    {
        public const int DiasMaximosDelPeriodo = 62;
        public const int LargoMaximoDeLaNota = 200;
        public const int RenglonesMaximos = 500;

        /// <summary>Las fechas de horas van de acá en adelante: un campo que falta llega como 0001-01-01 y no es un día de trabajo.</summary>
        public static readonly DateOnly FechaMinima = new(2000, 1, 1);
        /// <summary>Y hasta acá: más allá no hay período que recorrer (y el calendario de .NET termina en 9999).</summary>
        public static readonly DateOnly FechaMaxima = new(2100, 12, 31);

        private static readonly TimeSpan DesfaseDeArgentina = TimeSpan.FromHours(-3);

        /// <summary>El día de hoy en Argentina: a las 22:30 de allá ya es la madrugada siguiente en UTC, pero «hoy» sigue siendo el mismo día.</summary>
        public static DateOnly HoyEnArgentina(DateTimeOffset ahora) => DateOnly.FromDateTime(ahora.ToOffset(DesfaseDeArgentina).DateTime);

        /// <summary>Lunes = 1 … domingo = 7, como los turnos del calendario.</summary>
        public static int DiaDeLaSemana(DateOnly fecha) => fecha.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)fecha.DayOfWeek;

        /// <summary>
        /// Las horas que el calendario le asigna a ese entrenador en ese día: los turnos suyos del día de la semana que corresponde a la fecha.
        /// El calendario es de lunes a viernes, así que en sábado y domingo da 0.
        /// </summary>
        public static int Programadas(IEnumerable<(long EntrenadorId, int DiaSemana)> turnos, long entrenadorId, DateOnly fecha)
        {
            var dia = DiaDeLaSemana(fecha);
            return turnos.Count(t => t.EntrenadorId == entrenadorId && t.DiaSemana == dia);
        }

        /// <summary>Horas de 0 a 24 y de a cuarto de hora (0 = faltó).</summary>
        public static decimal ValidarHoras(decimal horas)
        {
            if (horas < 0 || horas > 24 || horas * 4 != decimal.Truncate(horas * 4))
                throw new ArgumentException("Las horas van de 0 a 24, de a cuarto de hora.");
            return horas;
        }

        /// <summary>Valor de la hora: vacío vale (todavía sin valor); si no, de 0 al máximo y con hasta 2 decimales (no se redondea en silencio).</summary>
        public static decimal? ValidarValorHora(decimal? valor)
        {
            if (valor is null) return null;
            var importe = ReglasDeDatos.Importe(valor.Value, "El valor de la hora");
            if (importe != Math.Round(importe, 2)) throw new ArgumentException("El valor de la hora admite hasta 2 decimales.");
            return importe;
        }

        /// <summary>Un día se carga hasta hoy: no se cargan horas que todavía no pasaron, ni de una fecha que falta o es absurda.</summary>
        public static void ValidarFecha(DateOnly fecha, DateOnly hoy)
        {
            if (fecha < FechaMinima) throw new ArgumentException("La fecha del día no es válida.");
            if (fecha > hoy) throw new ArgumentException("No se pueden cargar horas de un día que todavía no pasó.");
        }

        /// <summary>Un período va de <paramref name="desde"/> a <paramref name="hasta"/> (los dos incluidos) y no pasa de 62 días.</summary>
        public static void ValidarPeriodo(DateOnly desde, DateOnly hasta)
        {
            if (desde < FechaMinima || hasta > FechaMaxima) throw new ArgumentException("Las fechas del período no son válidas.");
            if (desde > hasta) throw new ArgumentException("La fecha desde no puede ser posterior a la fecha hasta.");
            if (hasta.DayNumber - desde.DayNumber + 1 > DiasMaximosDelPeriodo)
                throw new ArgumentException($"El período no puede pasar de {DiasMaximosDelPeriodo} días.");
        }

        /// <summary>La nota como se guarda: recortada, vacía es null, de hasta 200 caracteres y sin caracteres de control.</summary>
        public static string? LimpiarNota(string? nota) => ReglasDeDatos.TextoOpcional(nota, "La nota", LargoMaximoDeLaNota);

        /// <summary>
        /// Lo que se paga por un día: horas por valor de la hora, redondeado a 2 decimales con el medio centavo hacia arriba. Se redondea
        /// por día (no sobre la suma), así el total del período es la suma exacta de lo que dice cada día.
        /// </summary>
        public static decimal MontoDelDia(decimal horas, decimal valorHora) => Math.Round(horas * valorHora, 2, MidpointRounding.AwayFromZero);
    }
}
