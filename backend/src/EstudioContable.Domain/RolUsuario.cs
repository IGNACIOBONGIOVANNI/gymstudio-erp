namespace EstudioContable.Domain.Enums
{
    /// <summary>
    /// Roles del sistema. Determinan autorización a nivel de endpoint
    /// y, en el caso de Cliente, el alcance de los datos visibles
    /// (un usuario Cliente sólo debe ver los asientos de su propio ClienteId).
    /// </summary>
    public enum RolUsuario
    {
        Administrador,
        Contador,
        Cliente
    }
}
