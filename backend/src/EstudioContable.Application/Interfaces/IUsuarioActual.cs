namespace EstudioContable.Application.Interfaces
{
    /// <summary>Usuario autenticado en la request actual (null fuera de una request, ej. al arrancar).</summary>
    public interface IUsuarioActual
    {
        string? Email { get; }
    }
}
