using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EstudioContable.Application.Copias
{
    /// <summary>
    /// Lo que se envía para programar las copias automáticas. <paramref name="Frecuencia"/> es el nombre exacto de la frecuencia
    /// (<c>CadaSeisHoras</c>, <c>CadaDoceHoras</c>, <c>Diaria</c>, <c>Semanal</c> o <c>Mensual</c>); un número o cualquier otro texto se rechaza.
    /// </summary>
    public record ProgramacionDeCopias(bool Activa, string? Frecuencia, int Hora, int Minuto, int? DiaDeLaSemana, int? DiaDelMes, int Conservar);

    /// <summary>La programación vigente y cómo le fue a la última copia. Todas las horas son de Argentina.</summary>
    public record EstadoDeCopiasAutomaticas(
        bool Activa, string Frecuencia, int Hora, int Minuto, int? DiaDeLaSemana, int? DiaDelMes, int Conservar,
        DateTimeOffset? ProximaCopia, DateTimeOffset? UltimaCopia, DateTimeOffset? UltimoIntento, string? UltimoError);

    /// <summary>Una copia guardada en la carpeta de copias automáticas.</summary>
    public record CopiaGuardada(string Nombre, long Bytes, DateTimeOffset Fecha);

    /// <summary>
    /// Copias de seguridad automáticas: la programación (cada 6 h, cada 12 h, diaria, semanal o mensual), la tarea que las hace
    /// y la carpeta donde se guardan. Cada copia es la misma, firmada, que se descarga desde la pantalla (<see cref="ICopiasDeSeguridadService"/>).
    /// </summary>
    public interface ICopiasAutomaticasService
    {
        /// <summary>La programación y el estado de la última copia. Si la fila de configuración falta, la crea con los valores por defecto.</summary>
        Task<EstadoDeCopiasAutomaticas> ObtenerAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Cambia la programación (la valida entera y no cambia nada si algo no sirve: <see cref="ArgumentException"/>) y reinicia
        /// desde cuándo se cuenta la próxima copia. Queda en la auditoría.
        /// </summary>
        Task<EstadoDeCopiasAutomaticas> ProgramarAsync(ProgramacionDeCopias pedido, CancellationToken cancellationToken);

        /// <summary>
        /// Hace una copia en la carpeta en este momento. Cuenta como la última copia, pero no mueve la próxima programada. Si no se
        /// puede guardar lanza <see cref="InvalidOperationException"/> con un mensaje claro.
        /// </summary>
        Task<CopiaGuardada> GuardarAhoraAsync(CancellationToken cancellationToken);

        /// <summary>
        /// La vuelta de la tarea en segundo plano: si toca, hace la copia. Devuelve <c>null</c> si no tocaba, si ya había otra copia en
        /// curso o si falló. Una falla se guarda (y se reintenta a los 30 minutos) pero nunca se propaga.
        /// </summary>
        Task<CopiaGuardada?> EjecutarSiTocaAsync(CancellationToken cancellationToken);

        /// <summary>Las copias de la carpeta, de la más nueva a la más vieja. Solo las que se llaman exactamente como una copia automática.</summary>
        IReadOnlyList<CopiaGuardada> Listar();

        /// <summary>
        /// Abre una copia para descargarla. Devuelve <c>null</c> si el nombre no es el de una copia automática, o si el archivo no está en
        /// la carpeta: ningún nombre con barras, puntos dobles o rutas puede llegar a otro archivo.
        /// </summary>
        Stream? AbrirParaDescargar(string nombre);
    }
}
