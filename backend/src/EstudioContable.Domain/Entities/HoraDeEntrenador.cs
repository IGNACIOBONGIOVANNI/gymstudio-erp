using System;

namespace EstudioContable.Domain.Entities
{
    /// <summary>
    /// Lo que cumplió un entrenador en un día: las horas (0 = faltó) y una nota opcional. Guarda el <b>valor de la hora de ese momento</b>,
    /// copiado de la ficha al crear el día: si después el valor del entrenador sube, los días ya cargados siguen con el que tenían y el
    /// presupuesto de un período viejo no cambia. Hay un solo registro por entrenador y día (lo garantiza un índice único en la base).
    /// </summary>
    public class HoraDeEntrenador
    {
        public long Id { get; private set; }
        public long EntrenadorId { get; private set; }
        public DateOnly Fecha { get; private set; }
        public decimal Horas { get; private set; }
        public decimal ValorHora { get; private set; }
        public string? Nota { get; private set; }

        protected HoraDeEntrenador() { } // EF Core

        /// <param name="hoy">El día de hoy en Argentina: no se cargan días que todavía no pasaron.</param>
        public HoraDeEntrenador(long entrenadorId, DateOnly fecha, decimal horas, decimal valorHora, string? nota, DateOnly hoy)
        {
            if (entrenadorId <= 0) throw new ArgumentException("El entrenador es requerido.");
            ReglasDeHoras.ValidarFecha(fecha, hoy);
            var horasValidas = ReglasDeHoras.ValidarHoras(horas);
            var valorValido = ReglasDeHoras.ValidarValorHora(valorHora)!.Value;
            var notaLimpia = ReglasDeHoras.LimpiarNota(nota);

            EntrenadorId = entrenadorId;
            Fecha = fecha;
            Horas = horasValidas;
            ValorHora = valorValido;
            Nota = notaLimpia;
        }

        /// <summary>Cambia las horas y la nota. El valor de la hora y la fecha no cambian. Si algo es inválido no cambia nada.</summary>
        public void Cambiar(decimal horas, string? nota)
        {
            var horasValidas = ReglasDeHoras.ValidarHoras(horas);
            var notaLimpia = ReglasDeHoras.LimpiarNota(nota);
            Horas = horasValidas;
            Nota = notaLimpia;
        }
    }
}
