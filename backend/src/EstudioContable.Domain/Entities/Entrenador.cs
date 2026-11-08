using System;
using System.Text.RegularExpressions;

namespace EstudioContable.Domain.Entities
{
    /// <summary>
    /// Un entrenador (o coach) del gimnasio. Es una ficha, no un usuario: no tiene contraseña ni entra al sistema. Se carga para poder
    /// ponerlo en el calendario semanal. El nombre no se repite aunque cambien las mayúsculas o los espacios (lo garantiza también
    /// un índice único en la base) y no se borra: se desactiva, y solo si no tiene horas en el calendario.
    /// </summary>
    public class Entrenador
    {
        public const int LargoMaximoDelNombre = 80;
        public const int LargoMaximoDelTelefono = 30;
        public const int LargoMaximoDeLaEspecialidad = 60;

        // El espacio común y los de otros tipos (el duro que trae pegar desde WhatsApp o Word, el angosto, el largo, el ideográfico...):
        // cualquier racha pasa a un solo espacio común, así «Ana López» y «Ana López» con espacio duro son la misma ficha. El tabulador
        // y el salto de línea no están acá: son caracteres de control y el nombre que los trae se rechaza.
        private static readonly Regex EspaciosRepetidos = new(@"[    -     　]+", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        public long Id { get; private set; }
        public string NombreCompleto { get; private set; } = string.Empty;
        public string? Telefono { get; private set; }
        public string? Especialidad { get; private set; }
        public bool Activo { get; private set; } = true;
        public DateTimeOffset FechaCreacion { get; private set; } = DateTimeOffset.UtcNow;

        protected Entrenador() { } // EF Core

        public Entrenador(string? nombreCompleto, string? telefono, string? especialidad)
        {
            AsignarDatos(nombreCompleto, telefono, especialidad);
        }

        public void ActualizarDatos(string? nombreCompleto, string? telefono, string? especialidad)
            => AsignarDatos(nombreCompleto, telefono, especialidad);

        public void Activar() => Activo = true;

        public void Desactivar() => Activo = false;

        /// <summary>
        /// El nombre como se guarda: sin espacios sobrantes ni repetidos, obligatorio, de hasta 80 caracteres y sin caracteres de
        /// control. Las mayúsculas y los acentos se conservan; para comparar nombres se ignoran las mayúsculas.
        /// </summary>
        public static string LimpiarNombre(string? nombre)
            => ReglasDeDatos.TextoRequerido(EspaciosRepetidos.Replace(nombre ?? string.Empty, " "), "El nombre del entrenador", LargoMaximoDelNombre);

        // Se valida todo antes de asignar nada: un pedido inválido no deja la ficha a medias.
        private void AsignarDatos(string? nombreCompleto, string? telefono, string? especialidad)
        {
            var nombre = LimpiarNombre(nombreCompleto);
            var tel = ReglasDeDatos.TextoOpcional(telefono, "El teléfono", LargoMaximoDelTelefono);
            var esp = ReglasDeDatos.TextoOpcional(especialidad, "La especialidad", LargoMaximoDeLaEspecialidad);
            NombreCompleto = nombre;
            Telefono = tel;
            Especialidad = esp;
        }
    }
}
