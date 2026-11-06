namespace EstudioContable.Domain.Entities
{
    /// <summary>
    /// Quién está de encargado en el gimnasio un día de la semana y a una hora. Es un calendario semanal que se repite:
    /// no tiene fechas. Cada día y hora tiene como mucho una persona; una celda vacía no ocupa una fila.
    /// </summary>
    public class TurnoEncargado
    {
        public long Id { get; private set; }
        /// <summary>1 = lunes ... 5 = viernes.</summary>
        public int DiaSemana { get; private set; }
        /// <summary>Hora en que empieza el turno de una hora: 6 es de 06:00 a 07:00 y 21 es de 21:00 a 22:00.</summary>
        public int Hora { get; private set; }
        public string Encargado { get; private set; } = string.Empty;

        protected TurnoEncargado() { } // EF Core

        public TurnoEncargado(int diaSemana, int hora, string encargado)
        {
            ReglasDeCalendario.ValidarCelda(diaSemana, hora);
            DiaSemana = diaSemana;
            Hora = hora;
            Encargado = ReglasDeCalendario.NombreDelEncargado(encargado);
        }

        public void CambiarEncargado(string encargado) => Encargado = ReglasDeCalendario.NombreDelEncargado(encargado);
    }
}
