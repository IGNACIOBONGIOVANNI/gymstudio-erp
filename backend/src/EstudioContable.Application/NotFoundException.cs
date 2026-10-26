using System;

namespace EstudioContable.Application.Common.Exceptions
{
    /// <summary>
    /// Se lanza cuando una entidad solicitada por Id no existe.
    /// El middleware de la API debe mapearla a HTTP 404.
    /// </summary>
    public class NotFoundException : Exception
    {
        public NotFoundException(string entityName, object key)
            : base($"No se encontró '{entityName}' con clave '{key}'.")
        {
        }
    }
}
