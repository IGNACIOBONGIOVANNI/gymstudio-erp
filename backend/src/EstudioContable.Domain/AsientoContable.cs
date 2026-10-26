using System;
using System.Collections.Generic;
using System.Linq;
using EstudioContable.Domain.Enums;
using EstudioContable.Domain.Exceptions;

namespace EstudioContable.Domain.Entities
{
    /// <summary>
    /// Agregado raíz que representa un asiento de partida doble.
    /// Toda modificación a sus líneas debe pasar por los métodos de esta clase
    /// (nunca manipular LineaAsiento directamente desde fuera del dominio).
    /// </summary>
    public class AsientoContable
    {
        public long Id { get; private set; }
        public long ClienteId { get; private set; }
        public long NumeroAsiento { get; private set; }
        public DateOnly Fecha { get; private set; }
        public string Descripcion { get; private set; } = default!;
        public EstadoAsiento Estado { get; private set; } = EstadoAsiento.Borrador;
        public long UsuarioCreadorId { get; private set; }
        public DateTimeOffset FechaCreacion { get; private set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? FechaConfirmacion { get; private set; }

        // Auditoría: quién confirmó el asiento (distinto de quién lo creó).
        // Null mientras el asiento esté en Borrador o Anulado sin haber sido confirmado.
        public long? UsuarioConfirmacionId { get; private set; }

        private readonly List<LineaAsiento> _lineas = new();
        public IReadOnlyCollection<LineaAsiento> Lineas => _lineas.AsReadOnly();

        // Totales calculados en memoria: no se persisten como columnas,
        // siempre se derivan de las líneas para evitar inconsistencias.
        public decimal TotalDebe => _lineas.Sum(l => l.Debe);
        public decimal TotalHaber => _lineas.Sum(l => l.Haber);
        public bool EstaBalanceado => Math.Round(TotalDebe, 2) == Math.Round(TotalHaber, 2) && Math.Round(TotalDebe, 2) > 0;

        protected AsientoContable() { } // requerido por EF Core

        public AsientoContable(long clienteId, long numeroAsiento, DateOnly fecha, string descripcion, long usuarioCreadorId)
        {
            if (string.IsNullOrWhiteSpace(descripcion))
                throw new ArgumentException("La descripción del asiento es obligatoria.", nameof(descripcion));

            ClienteId = clienteId;
            NumeroAsiento = numeroAsiento;
            Fecha = fecha;
            Descripcion = descripcion;
            UsuarioCreadorId = usuarioCreadorId;
        }

        /// <summary>
        /// Agrega una línea al Debe o al Haber. Sólo permitido en estado Borrador,
        /// ya que un asiento Confirmado debe permanecer inmutable (trazabilidad legal).
        /// </summary>
        public void AgregarLinea(long cuentaId, decimal debe, decimal haber, string? descripcion = null)
        {
            if (Estado != EstadoAsiento.Borrador)
                throw new InvalidOperationException("Sólo se pueden agregar líneas a un asiento en estado Borrador.");

            _lineas.Add(new LineaAsiento(cuentaId, debe, haber, descripcion, orden: (short)_lineas.Count));
        }

        public void QuitarLinea(long lineaId)
        {
            if (Estado != EstadoAsiento.Borrador)
                throw new InvalidOperationException("Sólo se pueden quitar líneas de un asiento en estado Borrador.");

            _lineas.RemoveAll(l => l.Id == lineaId);
        }

        /// <summary>
        /// REGLA DE NEGOCIO CENTRAL: confirma el asiento sólo si la partida doble
        /// se cumple estrictamente (suma Debe == suma Haber). Si no balancea,
        /// lanza AsientoDesbalanceadoException y el asiento permanece en Borrador
        /// (ninguno de los campos de auditoría se toca en ese caso).
        /// </summary>
        /// <param name="usuarioConfirmacionId">
        /// Id del usuario (típicamente rol Contador/Administrador) que ejecuta la confirmación.
        /// Puede diferir del usuario que creó el asiento originalmente.
        /// </param>
        public void Confirmar(long usuarioConfirmacionId)
        {
            if (Estado != EstadoAsiento.Borrador)
                throw new InvalidOperationException("Sólo un asiento en Borrador puede confirmarse.");

            if (_lineas.Count < 2)
                throw new InvalidOperationException("Un asiento requiere al menos dos líneas (una al Debe y otra al Haber).");

            if (!EstaBalanceado)
                throw new AsientoDesbalanceadoException(TotalDebe, TotalHaber);

            Estado = EstadoAsiento.Confirmado;
            FechaConfirmacion = DateTimeOffset.UtcNow;
            UsuarioConfirmacionId = usuarioConfirmacionId;
        }

        /// <summary>
        /// Anula un asiento ya confirmado. No se elimina físicamente (auditoría):
        /// la reversión contable se hace generando un nuevo asiento de reverso.
        /// </summary>
        public void Anular()
        {
            if (Estado != EstadoAsiento.Confirmado)
                throw new InvalidOperationException("Sólo un asiento Confirmado puede anularse.");

            Estado = EstadoAsiento.Anulado;
        }
    }
}
