using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EstudioContable.Application.Entrenadores
{
    /// <summary>
    /// Un entrenador del período, con el valor de la hora que tiene hoy en su ficha (null si todavía no tiene) y si está activo: a un
    /// desactivado se le ven y corrigen las horas, pero no se le crean días nuevos.
    /// </summary>
    public record EntrenadorDeHoras(long Id, string Nombre, decimal? ValorHora, bool Activo);

    /// <summary>
    /// Un día de un entrenador. <paramref name="Programadas"/> son las horas que le tocaban según el calendario; <paramref name="Cumplidas"/> es
    /// null si todavía no se cargó el día. <paramref name="ValorHora"/> es el valor con el que se cargó ese día (null si no se cargó).
    /// </summary>
    public record DiaDeHoras(long EntrenadorId, DateOnly Fecha, int Programadas, decimal? Cumplidas, decimal? ValorHora, string? Nota);

    public record HorasDelPeriodo(IReadOnlyList<EntrenadorDeHoras> Entrenadores, IReadOnlyList<DiaDeHoras> Dias);

    /// <summary>Un renglón para guardar: <paramref name="Horas"/> null borra el día; el valor de la hora no viaja (lo pone el servidor).</summary>
    public record CambioDeHoras(long EntrenadorId, DateOnly Fecha, decimal? Horas, string? Nota);

    /// <summary>Cuántos días creó la precarga y a qué entrenadores activos les faltó el valor de la hora para poder cargarles algo.</summary>
    public record ResultadoDePrecarga(int Creados, IReadOnlyList<string> SinValor);

    /// <summary>
    /// Lo que se le paga a un entrenador en el período. <paramref name="ValorHora"/> es el valor de la hora si fue el mismo en todos los días con
    /// horas y null si hubo más de uno; <paramref name="Monto"/> siempre es la suma exacta del monto de cada día.
    /// </summary>
    public record FilaDePresupuesto(long EntrenadorId, string Nombre, int Dias, decimal Horas, decimal? ValorHora, decimal Monto);

    public record Presupuesto(DateOnly Desde, DateOnly Hasta, IReadOnlyList<FilaDePresupuesto> Filas, decimal TotalHoras, decimal TotalMonto);

    /// <summary>
    /// Las horas que cumplieron los entrenadores y el presupuesto de pago por período. Todo es solo para la gestión (lo exige el controlador).
    /// Las reglas (fecha no futura, horas de a cuarto de hora, período de hasta 62 días, monto de un día) están en <c>ReglasDeHoras</c>.
    /// </summary>
    public interface IHorasDeEntrenadoresService
    {
        /// <summary>Los entrenadores activos (y los desactivados que tienen horas en el período) y un renglón por cada día con horas programadas o ya cargado.</summary>
        Task<HorasDelPeriodo> ObtenerAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken);

        /// <summary>Crea, cambia o borra días. Todo o nada: si algo es inválido no se guarda ninguno.</summary>
        Task GuardarAsync(IReadOnlyList<CambioDeHoras> cambios, CancellationToken cancellationToken);

        /// <summary>Carga, hasta hoy, las horas programadas de los entrenadores activos con valor de hora, sin pisar lo ya cargado. Se puede repetir.</summary>
        Task<ResultadoDePrecarga> PrecargarAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken);

        Task<Presupuesto> PresupuestoAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken);
    }
}
