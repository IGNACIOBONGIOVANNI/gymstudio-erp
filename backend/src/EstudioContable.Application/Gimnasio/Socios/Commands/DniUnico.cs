using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Gimnasio.Socios.Commands
{
    /// <summary>Dos socios no pueden tener el mismo DNI.</summary>
    internal static class DniUnico
    {
        public static async Task ValidarAsync(IAppDbContext context, string? dni, long? idExcluido, CancellationToken cancellationToken)
        {
            var normalizado = Socio.NormalizarDni(dni);
            var existente = await context.Socios
                .Where(s => s.Dni == normalizado && s.Id != idExcluido)
                .Select(s => new { s.Apellido, s.Nombre })
                .FirstOrDefaultAsync(cancellationToken);

            if (existente != null)
                throw new InvalidOperationException($"Ya existe un socio con el DNI {normalizado}: {existente.Apellido} {existente.Nombre}.");
        }
    }
}
