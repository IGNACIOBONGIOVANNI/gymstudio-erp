using System;

namespace EstudioContable.Application.Gimnasio.Commands
{
    internal static class MetodoPago
    {
        /// <summary>"caja" -> ContadoCaja, "banco" -> TransferenciaBanco. Cualquier otro valor es un error.</summary>
        public static string ACondicionDePago(string? metodoPago) => metodoPago?.Trim().ToLowerInvariant() switch
        {
            "caja" => "ContadoCaja",
            "banco" => "TransferenciaBanco",
            _ => throw new ArgumentException("Método de pago inválido: debe ser \"caja\" o \"banco\".")
        };
    }
}
