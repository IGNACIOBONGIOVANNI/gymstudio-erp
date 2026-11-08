using System;

namespace EstudioContable.Domain.Entities
{
    /// <summary>
    /// Qué entrenador está a cargo del gimnasio un día de la semana y a una hora. Es un calendario semanal que se repite:
    /// no tiene fechas. Cada día y hora tiene como mucho un entrenador; una celda vacía no ocupa una fila.
    /// </summary>
    public class TurnoEncargado
    {
        public long Id { get; private set; }
        /// <summary>1 = lunes ... 5 = viernes.</summary>
        public int DiaSemana { get; private set; }
        /// <summary>Hora en que empieza el turno de una hora: 6 es de 06:00 a 07:00 y 21 es de 21:00 a 22:00.</summary>
        public int Hora { get; private set; }
        public long EntrenadorId { get; private set; }
        public Entrenador? Entrenador { get; private set; }

        protected TurnoEncargado() { } // EF Core

        public TurnoEncargado(int diaSemana, int hora, long entrenadorId)
        {
            ReglasDeCalendario.ValidarCelda(diaSemana, hora);
            DiaSemana = diaSemana;
            Hora = hora;
            EntrenadorId = ValidarEntrenador(entrenadorId);
        }

        public void CambiarEntrenador(long entrenadorId) => EntrenadorId = ValidarEntrenador(entrenadorId);

        private static long ValidarEntrenador(long entrenadorId)
        {
            if (entrenadorId <= 0) throw new ArgumentException("Elegí un entrenador para el turno.");
            return entrenadorId;
        }
    }
}
