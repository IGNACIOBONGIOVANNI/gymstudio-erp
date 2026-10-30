using System;

namespace EstudioContable.Domain.Entities
{
    /// <summary>
    /// Fila de la tabla de auditoría. La aplicación nunca la escribe: la completa el
    /// trigger contabilidad.fn_auditar() en la base de datos ante cada INSERT, UPDATE
    /// o DELETE de las tablas auditadas, así que registra cambios hechos por cualquier
    /// camino (la API, un script o un cliente SQL).
    /// </summary>
    public class RegistroAuditoria
    {
        public long Id { get; private set; }
        public string Tabla { get; private set; } = default!;

        // "INSERT", "UPDATE" o "DELETE"
        public string Operacion { get; private set; } = default!;
        public string? RegistroId { get; private set; }

        // Fila completa antes y después del cambio, en JSON (sin el hash de contraseña).
        public string? DatosAnteriores { get; private set; }
        public string? DatosNuevos { get; private set; }
        public string[] CamposModificados { get; private set; } = Array.Empty<string>();

        // Email del usuario logueado en la API (o el usuario de PostgreSQL si el
        // cambio no vino de la API).
        public string Usuario { get; private set; } = default!;
        public DateTimeOffset Fecha { get; private set; }

        // Equipo desde el que se hizo el cambio: IP, sistema operativo y navegador (los dos últimos salen
        // del User-Agent, que el propio equipo informa). Vacíos si el cambio no vino de la API, y en los
        // registros anteriores a que se guardaran.
        public string? Ip { get; private set; }
        public string? SistemaOperativo { get; private set; }
        public string? Navegador { get; private set; }

        protected RegistroAuditoria() { }
    }
}
