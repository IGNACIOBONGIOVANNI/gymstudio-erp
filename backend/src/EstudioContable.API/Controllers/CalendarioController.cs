using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.API.Auth;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain;
using EstudioContable.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.API.Controllers
{
    /// <summary>
    /// Calendario semanal de entrenadores a cargo del gimnasio (lunes a viernes, de 06 a 22 hs). Lo ven todos los usuarios, porque
    /// Recepción necesita saber quién está a cargo; lo modifican el Administrador y el Contador. Cada turno apunta a la ficha de un
    /// entrenador activo.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CalendarioController : ControllerBase
    {
        private readonly IAppDbContext _context;

        public CalendarioController(IAppDbContext context) => _context = context;

        /// <param name="Dia">1 = lunes ... 5 = viernes.</param>
        /// <param name="Hora">Hora en que empieza el turno de una hora (6 = de 06:00 a 07:00).</param>
        /// <param name="EntrenadorId">El entrenador a cargo. Al guardar solo se usa este dato.</param>
        /// <param name="Entrenador">Su nombre, tal como está en su ficha. Solo se informa al leer; al guardar se ignora.</param>
        public record TurnoDto(int Dia, int Hora, long EntrenadorId, string? Entrenador);

        public record GuardarCalendarioRequest(List<TurnoDto?>? Turnos);

        [HttpGet]
        public async Task<IActionResult> Obtener(CancellationToken cancellationToken)
            => Ok(await Listar(cancellationToken));

        /// <summary>
        /// Reemplaza el calendario por el que se envía: solo van las celdas con un entrenador. Se guardan solamente las diferencias
        /// (altas, cambios y bajas), en una sola operación: si algo falla no se cambia nada.
        /// </summary>
        [Authorize(Roles = Roles.Gestion)]
        [HttpPut]
        public async Task<IActionResult> Guardar([FromBody] GuardarCalendarioRequest request, CancellationToken cancellationToken)
        {
            var pedidos = request?.Turnos ?? throw new ArgumentException("Faltan los turnos del calendario.");
            if (pedidos.Count > ReglasDeCalendario.CeldasMaximas)
                throw new ArgumentException($"El calendario no puede tener más de {ReglasDeCalendario.CeldasMaximas} turnos: son los de una semana.");

            var nuevos = new Dictionary<(int Dia, int Hora), long>();
            foreach (var turno in pedidos)
            {
                if (turno == null) throw new ArgumentException("Hay un turno vacío en el calendario.");
                ReglasDeCalendario.ValidarCelda(turno.Dia, turno.Hora);
                if (!nuevos.TryAdd((turno.Dia, turno.Hora), turno.EntrenadorId))
                    throw new ArgumentException("Hay dos entrenadores cargados en el mismo día y a la misma hora.");
            }

            await ValidarEntrenadoresAsync(nuevos.Values.Distinct().ToList(), cancellationToken);

            var actuales = await _context.TurnosEncargado.ToListAsync(cancellationToken);
            foreach (var actual in actuales)
            {
                if (nuevos.Remove((actual.DiaSemana, actual.Hora), out var entrenadorId))
                {
                    if (actual.EntrenadorId != entrenadorId) actual.CambiarEntrenador(entrenadorId);
                }
                else
                {
                    _context.TurnosEncargado.Remove(actual);
                }
            }
            foreach (var ((dia, hora), entrenadorId) in nuevos)
                _context.TurnosEncargado.Add(new TurnoEncargado(dia, hora, entrenadorId));

            await _context.SaveChangesAsync(cancellationToken);
            return Ok(await Listar(cancellationToken));
        }

        /// <summary>Cada entrenador del pedido tiene que existir y estar activo: un nombre mal escrito o dado de baja no entra al calendario.</summary>
        private async Task ValidarEntrenadoresAsync(List<long> ids, CancellationToken cancellationToken)
        {
            if (ids.Count == 0) return;
            var fichas = await _context.Entrenadores.AsNoTracking()
                .Where(e => ids.Contains(e.Id))
                .Select(e => new { e.Id, e.NombreCompleto, e.Activo })
                .ToListAsync(cancellationToken);

            if (fichas.Count != ids.Count)
                throw new ArgumentException("Uno de los entrenadores no existe.");
            var desactivado = fichas.FirstOrDefault(f => !f.Activo);
            if (desactivado != null)
                throw new ArgumentException($"{desactivado.NombreCompleto} está desactivado: activalo en la pestaña Entrenadores o elegí a otra persona.");
        }

        private async Task<List<TurnoDto>> Listar(CancellationToken cancellationToken)
            => await _context.TurnosEncargado.AsNoTracking()
                .OrderBy(t => t.DiaSemana).ThenBy(t => t.Hora)
                .Select(t => new TurnoDto(t.DiaSemana, t.Hora, t.EntrenadorId, t.Entrenador!.NombreCompleto))
                .ToListAsync(cancellationToken);
    }
}
