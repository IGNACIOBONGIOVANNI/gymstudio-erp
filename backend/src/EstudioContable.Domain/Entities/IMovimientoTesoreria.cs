using System;

namespace EstudioContable.Domain.Entities
{
    /// <summary>
    /// Lo común a un movimiento de Caja y de Banco, para que el servicio de tesorería
    /// trate los dos libros igual.
    /// </summary>
    public interface IMovimientoTesoreria
    {
        long Id { get; }
        DateTime Fecha { get; }
        string Detalle { get; }
        decimal Importe { get; }
        bool EsIngreso { get; }

        // Factura que lo generó (null si se cargó a mano en el libro).
        long? FacturaId { get; }

        // Asiento que registra el movimiento en la contabilidad (sólo los manuales;
        // los de una factura se registran en el asiento de la factura).
        long? AsientoContableId { get; }

        // Cliente al que se le cobró o proveedor al que se le pagó (sólo cobros y pagos a cuenta corriente).
        long? ClienteComercialId { get; }
        long? ProveedorId { get; }

        void AsignarTercero(long? clienteComercialId, long? proveedorId);
        void ActualizarDatos(DateTime fecha, string detalle, bool esIngreso, decimal importe);
        void AsignarAsiento(long asientoId);
    }
}
