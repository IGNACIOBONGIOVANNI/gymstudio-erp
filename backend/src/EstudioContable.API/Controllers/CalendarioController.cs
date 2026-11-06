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
    /// Calendario semanal de encargados del gimnasio (lunes a viernes, de 06 a 22 hs). Lo ven todos los usuarios, porque
    /// Recepción necesita saber quién está de encargado; lo modifican el Administrador y el Contador.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CalendarioController : ControllerBase
    {
        private readonly IAppDbContext _context;

        public CalendarioController(IAppDbContext context) => _context = context;

        /// <param name="Dia">1 = lunes ... 5 = viernes.</param>
        /// <param name="Hora">Hora en que empieza el turno de una hora (6 = de 06:00 a 07:00).</param>
        public record TurnoDto(int Dia, int Hora, string? Encargado);

        public record GuardarCalendarioRequest(List<TurnoDto?>? Turnos);

        [HttpGet]
        public async Task<IActionResult> Obtener(CancellationToken cancellationToken)
            => Ok(await Listar(cancellationToken));

        /// <summary>
        /// Reemplaza el calendario por el que se envía: solo van las celdas con una persona. Se guardan solamente las diferencias
        /// (altas, cambios y bajas), en una sola operación: si algo falla no se cambia nada.
        /// </summary>
        [Authorize(Roles = Roles.Gestion)]
        [HttpPut]
        public async Task<IActionResult> Guardar([FromBody] GuardarCalendarioRequest request, CancellationToken cancellationToken)
        {
            var pedidos = request?.Turnos ?? throw new ArgumentException("Faltan los turnos del calendario.");
            if (pedidos.Count > ReglasDeCalendario.CeldasMaximas)
                throw new ArgumentException($"El calendario no puede tener más de {ReglasDeCalendario.CeldasMaximas} turnos: son los de una semana.");

            var nuevos = new Dictionary<(int Dia, int Hora), string>();
            foreach (var turno in pedidos)
            {
                if (turno == null) throw new ArgumentException("Hay un turno vacío en el calendario.");
                ReglasDeCalendario.ValidarCelda(turno.Dia, turno.Hora);
                var nombre = ReglasDeCalendario.NombreDelEncargado(turno.Encargado);
                if (!nuevos.TryAdd((turno.Dia, turno.Hora), nombre))
                    throw new ArgumentException("Hay dos encargados cargados en el mismo día y a la misma hora.");
            }

            var actuales = await _context.TurnosEncargado.ToListAsync(cancellationToken);
            foreach (var actual in actuales)
            {
                if (nuevos.Remove((actual.DiaSemana, actual.Hora), out var nombre))
                {
                    if (actual.Encargado != nombre) actual.CambiarEncargado(nombre);
                }
                else
                {
                    _context.TurnosEncargado.Remove(actual);
                }
            }
            foreach (var ((dia, hora), nombre) in nuevos)
                _context.TurnosEncargado.Add(new TurnoEncargado(dia, hora, nombre));

            await _context.SaveChangesAsync(cancellationToken);
            return Ok(await Listar(cancellationToken));
        }

        private async Task<List<TurnoDto>> Listar(CancellationToken cancellationToken)
            => await _context.TurnosEncargado.AsNoTracking()
                .OrderBy(t => t.DiaSemana).ThenBy(t => t.Hora)
                .Select(t => new TurnoDto(t.DiaSemana, t.Hora, t.Encargado))
                .ToListAsync(cancellationToken);
    }
}
