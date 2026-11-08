using System;

namespace EstudioContable.Domain.Entities
{
    /// <summary>
    /// La foto de un entrenador: a lo sumo una por entrenador. Vive en su propia tabla, aparte de la ficha, para que la lista de
    /// entrenadores y el calendario no carguen bytes de imágenes y para que la auditoría no copie la foto en cada cambio de la ficha.
    /// </summary>
    public class FotoDeEntrenador
    {
        public long EntrenadorId { get; private set; }
        public byte[] Foto { get; private set; } = Array.Empty<byte>();
        public DateTimeOffset Actualizada { get; private set; }

        protected FotoDeEntrenador() { } // EF Core

        public FotoDeEntrenador(long entrenadorId, byte[] foto, DateTimeOffset ahora)
        {
            if (entrenadorId <= 0) throw new ArgumentException("El entrenador de la foto es requerido.");
            ReglasDeFoto.Validar(foto);
            EntrenadorId = entrenadorId;
            Foto = foto;
            Actualizada = ahora;
        }

        /// <summary>Cambia la foto. Si el archivo no es aceptable lanza <see cref="ArgumentException"/> y no cambia nada.</summary>
        public void Reemplazar(byte[] foto, DateTimeOffset ahora)
        {
            ReglasDeFoto.Validar(foto);
            Foto = foto;
            Actualizada = ahora;
        }
    }
}
