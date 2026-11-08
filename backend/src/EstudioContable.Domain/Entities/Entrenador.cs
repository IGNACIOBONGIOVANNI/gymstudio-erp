using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace EstudioContable.Domain.Entities
{
    /// <summary>
    /// Un entrenador (o coach) del gimnasio. Es una ficha, no un usuario: no tiene contraseña ni entra al sistema. Se carga para poder
    /// ponerlo en el calendario semanal. El nombre no se repite aunque cambien las mayúsculas o los espacios (lo garantiza también
    /// un índice único en la base) y no se borra: se desactiva, y solo si no tiene horas en el calendario. Además del teléfono, la ficha
    /// puede llevar un email y un WhatsApp de contacto (la foto vive aparte, en <see cref="FotoDeEntrenador"/>) y el valor de la hora con
    /// el que se le paga (que solo ve y carga la gestión; lo oculta la API).
    /// </summary>
    public class Entrenador
    {
        public const int LargoMaximoDelNombre = 80;
        public const int LargoMaximoDelTelefono = 30;
        public const int LargoMaximoDeLaEspecialidad = 60;
        public const int LargoMaximoDelEmail = 254;
        public const int LargoMaximoDelWhatsApp = 30;

        // El espacio común y los de otros tipos (el duro que trae pegar desde WhatsApp o Word, el angosto, el largo, el ideográfico...):
        // cualquier racha pasa a un solo espacio común, así «Ana López» y «Ana López» con espacio duro son la misma ficha. El tabulador
        // y el salto de línea no están acá: son caracteres de control y el texto que los trae se rechaza.
        private static readonly Regex EspaciosRepetidos = new(
            @"[ \u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]+", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        // algo@dominio.ext: sin espacios, una sola arroba y un dominio con al menos un punto y sin partes vacías («a@b», «a@b..com» no valen).
        private static readonly Regex FormaDeEmail = new(
            @"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+\z", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        private static readonly Regex CaracteresDeWhatsApp = new(@"^[0-9 +\-()]+\z", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        public long Id { get; private set; }
        public string NombreCompleto { get; private set; } = string.Empty;
        public string? Telefono { get; private set; }
        public string? Especialidad { get; private set; }
        public string? Email { get; private set; }
        public string? WhatsApp { get; private set; }
        /// <summary>Lo que se le paga por hora; null = todavía sin valor.</summary>
        public decimal? ValorHora { get; private set; }
        public bool Activo { get; private set; } = true;
        public DateTimeOffset FechaCreacion { get; private set; } = DateTimeOffset.UtcNow;

        protected Entrenador() { } // EF Core

        public Entrenador(string? nombreCompleto, string? telefono, string? especialidad, string? email = null, string? whatsApp = null, decimal? valorHora = null)
        {
            AsignarDatos(nombreCompleto, telefono, especialidad, email, whatsApp, valorHora);
        }

        /// <summary>Reemplaza los datos de la ficha. El email, el WhatsApp y el valor de la hora que no se pasan quedan en blanco, como cualquier otro dato vacío.</summary>
        public void ActualizarDatos(string? nombreCompleto, string? telefono, string? especialidad, string? email = null, string? whatsApp = null, decimal? valorHora = null)
            => AsignarDatos(nombreCompleto, telefono, especialidad, email, whatsApp, valorHora);

        public void Activar() => Activo = true;

        public void Desactivar() => Activo = false;

        /// <summary>
        /// El nombre como se guarda: sin espacios sobrantes ni repetidos, obligatorio, de hasta 80 caracteres y sin caracteres de
        /// control. Las mayúsculas y los acentos se conservan; para comparar nombres se ignoran las mayúsculas.
        /// </summary>
        public static string LimpiarNombre(string? nombre)
            => ReglasDeDatos.TextoRequerido(ColapsarEspacios(nombre), "El nombre del entrenador", LargoMaximoDelNombre);

        /// <summary>
        /// El email como se guarda: sin espacios en los extremos, opcional (vacío es null), de hasta 254 caracteres y con forma de
        /// <c>algo@dominio.ext</c>. Las mayúsculas se conservan como se escribieron.
        /// </summary>
        public static string? LimpiarEmail(string? email)
        {
            var texto = ReglasDeDatos.TextoOpcional(email, "El email", LargoMaximoDelEmail);
            if (texto is not null && !FormaDeEmail.IsMatch(texto))
                throw new ArgumentException("El email no tiene un formato válido (por ejemplo, nombre@dominio.com).");
            return texto;
        }

        /// <summary>
        /// El WhatsApp como se guarda: opcional (vacío es null), de hasta 30 caracteres, con espacios comunes (el espacio duro de pegar
        /// desde el teléfono pasa a un espacio común) y solo con números, espacios y los signos + - ( ).
        /// </summary>
        public static string? LimpiarWhatsApp(string? whatsApp)
        {
            var texto = ReglasDeDatos.TextoOpcional(ColapsarEspacios(whatsApp), "El WhatsApp", LargoMaximoDelWhatsApp);
            if (texto is not null && (!CaracteresDeWhatsApp.IsMatch(texto) || !texto.Any(char.IsDigit)))
                throw new ArgumentException("El WhatsApp solo puede tener números, espacios y los signos + - ( ).");
            return texto;
        }

        private static string ColapsarEspacios(string? texto) => EspaciosRepetidos.Replace(texto ?? string.Empty, " ");

        // Se valida todo antes de asignar nada: un pedido inválido no deja la ficha a medias.
        private void AsignarDatos(string? nombreCompleto, string? telefono, string? especialidad, string? email, string? whatsApp, decimal? valorHora)
        {
            var nombre = LimpiarNombre(nombreCompleto);
            var tel = ReglasDeDatos.TextoOpcional(telefono, "El teléfono", LargoMaximoDelTelefono);
            var esp = ReglasDeDatos.TextoOpcional(especialidad, "La especialidad", LargoMaximoDeLaEspecialidad);
            var correo = LimpiarEmail(email);
            var whats = LimpiarWhatsApp(whatsApp);
            var valor = ReglasDeHoras.ValidarValorHora(valorHora);
            NombreCompleto = nombre;
            Telefono = tel;
            Especialidad = esp;
            Email = correo;
            WhatsApp = whats;
            ValorHora = valor;
        }
    }
}
