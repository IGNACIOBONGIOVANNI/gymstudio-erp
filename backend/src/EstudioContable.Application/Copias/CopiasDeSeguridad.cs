using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EstudioContable.Application.Copias
{
    /// <summary>Versión del sistema y de la base en un momento: la de la copia o la de ahora.</summary>
    public record EstadoDeSistema(string Version, string Migracion, int Migraciones);

    /// <summary>
    /// Una tabla comparada entre la copia y el sistema actual. Estado: "igual", "cambia", "solo-copia" (ya no existe
    /// en el sistema) o "solo-ahora" (no estaba en la copia).
    /// </summary>
    public record TablaComparada(
        string Tabla, string Etiqueta, string Grupo, int Orden,
        long? FilasCopia, long? FilasAhora, string Estado, bool EsAuditoria);

    /// <summary>
    /// Qué pasaría al cargar una copia, sin haber cambiado nada. Veredicto: "identica" (los datos son los mismos que ahora),
    /// "anterior" (se cargaron o cambiaron datos después de la copia) u "otra-version" (el esquema de la base no es el mismo).
    /// Firma: "valida" (la hizo un sistema con la misma clave y no se tocó), "invalida" (se modificó o la hizo otra instalación),
    /// "sin-firma" (el manifiesto no la trae) o "no-disponible" (este sistema no tiene clave para firmar ni verificar).
    /// </summary>
    public record ComparacionDeCopia(
        DateTimeOffset? Fecha,
        EstadoDeSistema Copia,
        EstadoDeSistema Actual,
        bool MismoEsquema,
        IReadOnlyList<TablaComparada> Tablas,
        string Veredicto,
        bool PuedeRestaurar,
        string? MotivoSiNoPuede,
        string Firma);

    public record ResultadoDeRestauracion(int Tablas, long Filas);

    /// <summary>
    /// Copia de seguridad completa del sistema desde la propia aplicación: todas las tablas (facturas, asientos, caja, banco,
    /// stock, socios, usuarios y auditoría) en un .zip con un manifiesto que dice la versión, la migración y la huella de
    /// cada tabla. Se puede comparar con el sistema actual (solo lectura) y restaurar.
    /// </summary>
    public interface ICopiasDeSeguridadService
    {
        /// <summary>Genera la copia (un .zip) con una foto consistente de la base.</summary>
        Task<byte[]> GenerarAsync(CancellationToken cancellationToken);

        /// <summary>Verifica la copia y la compara con el sistema actual. No modifica nada.</summary>
        Task<ComparacionDeCopia> CompararAsync(Stream zip, CancellationToken cancellationToken);

        /// <summary>
        /// Reemplaza TODOS los datos por los de la copia, en una sola transacción: si algo falla no cambia nada.
        /// Solo acepta una copia del mismo esquema de base y que pase la verificación de integridad.
        /// </summary>
        Task<ResultadoDeRestauracion> RestaurarAsync(Stream zip, CancellationToken cancellationToken);
    }

    /// <summary>Nombres legibles de las tablas, para mostrarlas agrupadas por módulo.</summary>
    public static class EtiquetasDeTablas
    {
        private static readonly Dictionary<string, (string Etiqueta, string Grupo, int Orden)> Conocidas = new(StringComparer.Ordinal)
        {
            ["contabilidad.Facturas"] = ("Facturas", "Ventas y compras", 10),
            ["contabilidad.FacturaItems"] = ("Ítems de las facturas", "Ventas y compras", 11),
            ["contabilidad.FacturaDetalles"] = ("Detalle de facturas (circuito viejo)", "Ventas y compras", 12),
            ["ventas.facturas_venta"] = ("Libro de ventas (circuito viejo)", "Ventas y compras", 13),
            ["compras.facturas_compra"] = ("Libro de compras (circuito viejo)", "Ventas y compras", 14),
            ["contabilidad.ClientesComerciales"] = ("Clientes", "Ventas y compras", 15),
            ["contabilidad.Proveedores"] = ("Proveedores", "Ventas y compras", 16),

            ["contabilidad.asientos_contables"] = ("Asientos contables", "Contabilidad", 20),
            ["contabilidad.lineas_asiento"] = ("Líneas de los asientos", "Contabilidad", 21),
            ["contabilidad.CuentasContables"] = ("Plan de cuentas", "Contabilidad", 22),
            ["contabilidad.Ejercicios"] = ("Ejercicios", "Contabilidad", 23),
            ["contabilidad.Cliente"] = ("Empresa", "Contabilidad", 24),

            ["caja.movimientos_caja"] = ("Movimientos de caja", "Tesorería", 30),
            ["banco.movimientos_banco"] = ("Movimientos de banco", "Tesorería", 31),

            ["inventario.Productos"] = ("Productos", "Stock", 40),
            ["inventario.MovimientosStock"] = ("Movimientos de stock", "Stock", 41),

            ["contabilidad.Socios"] = ("Socios", "Gimnasio", 50),
            ["contabilidad.Planes"] = ("Planes", "Gimnasio", 51),
            ["contabilidad.Membresias"] = ("Membresías", "Gimnasio", 52),
            ["contabilidad.AsistenciasSocio"] = ("Ingresos de socios", "Gimnasio", 53),
            ["contabilidad.MedicionesSocio"] = ("Mediciones de peso y medidas", "Gimnasio", 54),
            ["contabilidad.Rutinas"] = ("Rutinas modelo", "Gimnasio", 55),
            ["contabilidad.RutinaEjercicios"] = ("Ejercicios de las rutinas", "Gimnasio", 56),
            ["contabilidad.AsignacionesRutina"] = ("Rutinas asignadas a socios", "Gimnasio", 57),
            ["contabilidad.TurnosEncargado"] = ("Calendario de entrenadores", "Gimnasio", 58),
            ["contabilidad.Entrenadores"] = ("Entrenadores", "Gimnasio", 59),

            ["contabilidad.Usuario"] = ("Usuarios", "Sistema", 60),
            ["contabilidad.auditoria"] = ("Auditoría", "Sistema", 61),
            ["public.__EFMigrationsHistory"] = ("Historial de migraciones", "Sistema", 62),
            ["contabilidad.ConfiguracionDeCopias"] = ("Programación de copias automáticas", "Sistema", 63),
        };

        public static (string Etiqueta, string Grupo, int Orden) De(string tabla)
            => Conocidas.TryGetValue(tabla, out var datos) ? datos : (tabla, "Otras tablas", 99);

        public const string TablaDeAuditoria = "contabilidad.auditoria";
        public const string TablaDeMigraciones = "public.__EFMigrationsHistory";
    }
}
