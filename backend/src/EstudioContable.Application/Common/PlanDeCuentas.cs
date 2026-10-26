namespace EstudioContable.Application.Common
{
    /// <summary>
    /// Ids de las cuentas contables del plan de cuentas sembrado en AppDbContext
    /// (HasData). Centralizados acá para no repetir números mágicos ni buscar
    /// cuentas por nombre en cada caso de uso.
    /// </summary>
    public static class PlanDeCuentas
    {
        // El sistema hoy opera con un único cliente (el gimnasio) y sin login real.
        public const long ClienteIdPorDefecto = 1;
        public const long UsuarioSistemaId = 1;

        public const long Caja = 101;
        public const long Banco = 102;
        public const long DeudoresPorVentas = 103;
        public const long Ventas = 104;
        public const long IvaCreditoFiscal = 105;
        public const long IvaDebitoFiscal = 201;
        public const long Proveedores = 202;
        public const long Compras = 501;
    }
}
