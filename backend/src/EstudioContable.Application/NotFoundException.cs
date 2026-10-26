using System;
using System.Collections.Generic;

namespace EstudioContable.Application.Common.Exceptions
{
    /// <summary>
    /// Se lanza cuando una entidad solicitada por Id no existe.
    /// El middleware de la API la mapea a HTTP 404.
    /// </summary>
    public class NotFoundException : Exception
    {
        // Nombre de la entidad (nameof o texto libre) -> cómo se lo nombra en el mensaje.
        private static readonly Dictionary<string, string> Nombres = new(StringComparer.OrdinalIgnoreCase)
        {
            ["AsientoContable"] = "el asiento",
            ["Factura"] = "la factura",
            ["Factura de compra"] = "la factura de compra",
            ["Producto"] = "el producto",
            ["Usuario"] = "el usuario",
            ["Cliente"] = "el cliente",
            ["Socio"] = "el socio",
            ["Proveedor"] = "el proveedor",
            ["Plan"] = "el plan",
            ["Membresía"] = "la membresía",
            ["Movimiento de caja"] = "el movimiento de caja",
            ["Movimiento de banco"] = "el movimiento de banco",
        };

        public NotFoundException(string entityName, object key)
            : base($"No se encontró {(Nombres.TryGetValue(entityName, out var nombre) ? nombre : entityName)} (Id {key}).")
        {
        }
    }
}
