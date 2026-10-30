using System;
using System.Collections.Generic;
using System.Linq;
using EstudioContable.Domain.Enums;

namespace EstudioContable.Domain
{
    /// <summary>
    /// Formato de los datos de una factura cargada a mano o importada: tipo, número, nombre, documento,
    /// fecha e importes. El circuito de Facturar arma estos datos solo; esto protege los otros dos caminos.
    /// </summary>
    public static class ReglasDeComprobante
    {
        public const int LargoMaximoDelNombre = 200;      // igual que la columna
        public static readonly DateTime FechaMinima = new(2000, 1, 1);
        public static readonly DateTime FechaMaxima = new(2100, 12, 31);

        public static bool EsTipoValido(string? tipo)
            => !string.IsNullOrWhiteSpace(tipo) && tipo.Trim().ToUpperInvariant() is "A" or "B" or "C";

        public static string NormalizarTipo(string tipo) => tipo.Trim().ToUpperInvariant();

        /// <summary>Punto de venta y número, como los numera el sistema: 0001-00000001.</summary>
        public static bool EsNumeroValido(string? numero)
        {
            if (numero is not { Length: 13 }) return false;
            return numero.Take(4).All(char.IsAsciiDigit) && numero[4] == '-' && numero.Skip(5).All(char.IsAsciiDigit);
        }

        /// <summary>El primer problema de los datos de la cabecera, o null si están bien.</summary>
        public static string? ValidarCabecera(DateTime fecha, string? tipo, string? numero, string? nombre, string? documento)
        {
            if (fecha < FechaMinima || fecha > FechaMaxima) return "La fecha de la factura no es válida.";
            if (!EsTipoValido(tipo)) return "El tipo de factura tiene que ser A, B o C.";
            if (!EsNumeroValido(numero?.Trim())) return "El número de factura tiene que tener el formato 0001-00000001.";
            var limpio = (nombre ?? string.Empty).Trim();
            if (limpio.Length == 0) return "El nombre es requerido.";
            if (limpio.Length > LargoMaximoDelNombre) return $"El nombre no puede tener más de {LargoMaximoDelNombre} caracteres.";
            if (limpio.Any(char.IsControl)) return "El nombre tiene caracteres no permitidos.";
            if (!Cuit.EsDocumentoValido(documento)) return "El CUIT o DNI no es válido.";
            return null;
        }

        public const int ItemsMaximos = 200;
        public const int CantidadMaximaPorItem = 100_000;

        /// <summary>El primer problema de una factura cargada a mano (cliente, condición de pago e ítems), o null.</summary>
        public static string? ValidarFacturaManual(long clienteId, CondicionPago condicionPago, IReadOnlyCollection<(long ProductoId, int Cantidad)>? items)
        {
            if (clienteId <= 0) return "El cliente no es válido.";
            if (!Enum.IsDefined(condicionPago)) return "La condición de pago no es válida.";
            if (items == null || items.Count == 0 || items.Count > ItemsMaximos) return $"La factura tiene que tener entre 1 y {ItemsMaximos} ítems.";
            foreach (var (productoId, cantidad) in items)
            {
                if (productoId <= 0) return "Un ítem no tiene un producto válido.";
                if (cantidad <= 0 || cantidad > CantidadMaximaPorItem) return $"La cantidad de cada ítem tiene que estar entre 1 y {CantidadMaximaPorItem:N0}.";
            }
            return null;
        }

        /// <summary>El primer problema de los importes, o null si están bien. El total tiene que ser neto más IVA.</summary>
        public static string? ValidarImportes(decimal neto, decimal iva, decimal total)
        {
            foreach (var (valor, nombre) in new[] { (neto, "El neto"), (iva, "El IVA"), (total, "El total") })
            {
                if (valor < 0) return $"{nombre} no puede ser negativo.";
                if (valor > ReglasDeDatos.ImporteMaximo) return $"{nombre} es demasiado grande.";
            }
            // Un peso de tolerancia: las planillas reales redondean.
            if (Math.Abs(neto + iva - total) > 1m) return "El total no coincide con el neto más el IVA.";
            return null;
        }
    }
}
