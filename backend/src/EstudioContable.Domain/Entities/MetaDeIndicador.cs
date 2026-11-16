using System;

namespace EstudioContable.Domain.Entities
{
    /// <summary>
    /// La meta que el Administrador fija para un indicador del Tablero de Gestión (ciclo de control: meta, valor real, desvío).
    /// Una fila por indicador; la clave tiene que ser una del catálogo <see cref="TableroDeGestion"/> que admita meta.
    /// </summary>
    public class MetaDeIndicador
    {
        public const int LargoMaximoDeLaClave = 40;
        public const int LargoMaximoDelUsuario = 100;

        public string Clave { get; private set; } = string.Empty;
        public decimal Valor { get; private set; }
        public string ActualizadoPor { get; private set; } = string.Empty;
        public DateTime ActualizadoEn { get; private set; }

        protected MetaDeIndicador() { } // EF Core

        public MetaDeIndicador(string clave, decimal valor, string usuario, DateTime ahora)
        {
            var definicion = TableroDeGestion.Buscar(clave)
                ?? throw new ArgumentException($"«{clave}» no es un indicador del tablero.");
            if (!definicion.AdmiteMeta)
                throw new ArgumentException($"El indicador «{definicion.Nombre}» no lleva meta: es una foto del momento.");
            Clave = definicion.Clave;
            Cambiar(valor, usuario, ahora);
        }

        public void Cambiar(decimal valor, string usuario, DateTime ahora)
        {
            var definicion = TableroDeGestion.Buscar(Clave)!;
            if (valor < 0)
                throw new ArgumentException("La meta no puede ser negativa.");
            if (definicion.EsPorcentaje && valor > TableroDeGestion.MaximoPorcentaje)
                throw new ArgumentException($"La meta de «{definicion.Nombre}» es un porcentaje: no puede pasar de {TableroDeGestion.MaximoPorcentaje:0}.");
            if (valor > 999_999_999_999m)
                throw new ArgumentException("La meta es demasiado grande.");
            Valor = Math.Round(valor, 2);
            ActualizadoPor = string.IsNullOrWhiteSpace(usuario) ? "desconocido" : usuario.Trim()[..Math.Min(usuario.Trim().Length, LargoMaximoDelUsuario)];
            ActualizadoEn = DateTime.SpecifyKind(ahora, DateTimeKind.Utc);
        }
    }
}
