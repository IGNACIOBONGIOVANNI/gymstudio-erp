using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.API.Auth;
using EstudioContable.Application.Common.Exceptions;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain;
using EstudioContable.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace EstudioContable.API.Controllers
{
    /// <summary>
    /// Entrenadores del gimnasio: fichas con sus datos de contacto y su foto, que se ponen en el calendario semanal (no son usuarios
    /// del sistema). Las ven todos los usuarios; las cargan, editan, activan y desactivan —y les ponen o sacan la foto— el
    /// Administrador y el Contador. Un entrenador no se borra: se desactiva, y solo si no tiene horas en el calendario.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class EntrenadoresController : ControllerBase
    {
        // Un poco más que la foto más grande que se acepta (ReglasDeFoto.BytesMaximos): el tope del pedido corta lo que sobra.
        private const long TamanoMaximoDelPedidoDeFoto = 160_000;

        private readonly IAppDbContext _context;
        private readonly IRegistroDeEventos _eventos;
        private readonly IUsuarioActual _usuario;
        private readonly TimeProvider _reloj;

        public EntrenadoresController(IAppDbContext context, IRegistroDeEventos eventos, IUsuarioActual usuario, TimeProvider reloj)
        {
            _context = context;
            _eventos = eventos;
            _usuario = usuario;
            _reloj = reloj;
        }

        public record EntrenadorDto(long Id, string Nombre, string? Telefono, string? Especialidad, string? Email, string? WhatsApp,
            bool Activo, int HorasSemanales, bool TieneFoto, DateTimeOffset? FotoActualizada, decimal? ValorHora);

        public record GuardarEntrenadorRequest(string? Nombre, string? Telefono, string? Especialidad, string? Email, string? WhatsApp, decimal? ValorHora);

        public record CambiarEstadoRequest(bool Activo);

        [HttpGet]
        public async Task<IActionResult> Listar([FromQuery] bool soloActivos, CancellationToken cancellationToken)
        {
            var consulta = _context.Entrenadores.AsNoTracking();
            if (soloActivos) consulta = consulta.Where(e => e.Activo);
            var fichas = await consulta.OrderBy(e => e.NombreCompleto).ToListAsync(cancellationToken);

            var horas = await _context.TurnosEncargado.AsNoTracking()
                .GroupBy(t => t.EntrenadorId)
                .Select(g => new { EntrenadorId = g.Key, Horas = g.Count() })
                .ToDictionaryAsync(g => g.EntrenadorId, g => g.Horas, cancellationToken);

            // Solo el id y la fecha: la lista nunca carga los bytes de las fotos (se piden de a una, por su propia ruta).
            var fotos = await _context.FotosDeEntrenador.AsNoTracking()
                .Select(f => new { f.EntrenadorId, f.Actualizada })
                .ToDictionaryAsync(f => f.EntrenadorId, f => f.Actualizada, cancellationToken);

            // El valor de la hora es dinero: lo ven solo el Administrador y el Contador. Para Recepción lo oculta el servidor (null), no la pantalla.
            var veElValor = User.IsInRole(Roles.Administrador) || User.IsInRole(Roles.Contador);

            return Ok(fichas.Select(e => new EntrenadorDto(
                e.Id, e.NombreCompleto, e.Telefono, e.Especialidad, e.Email, e.WhatsApp, e.Activo, horas.GetValueOrDefault(e.Id),
                fotos.ContainsKey(e.Id), fotos.TryGetValue(e.Id, out var actualizada) ? actualizada : null,
                veElValor ? e.ValorHora : null)));
        }

        [Authorize(Roles = Roles.Gestion)]
        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] GuardarEntrenadorRequest request, CancellationToken cancellationToken)
        {
            var entrenador = new Entrenador(request.Nombre, request.Telefono, request.Especialidad, request.Email, request.WhatsApp, request.ValorHora);
            await ValidarNombreLibreAsync(entrenador.NombreCompleto, idExcluido: null, cancellationToken);

            _context.Entrenadores.Add(entrenador);
            await _context.SaveChangesAsync(cancellationToken);
            return Created($"/api/entrenadores/{entrenador.Id}", new { id = entrenador.Id });
        }

        [Authorize(Roles = Roles.Gestion)]
        [HttpPut("{id:long}")]
        public async Task<IActionResult> Actualizar(long id, [FromBody] GuardarEntrenadorRequest request, CancellationToken cancellationToken)
        {
            var entrenador = await BuscarAsync(id, cancellationToken);
            entrenador.ActualizarDatos(request.Nombre, request.Telefono, request.Especialidad, request.Email, request.WhatsApp, request.ValorHora);
            await ValidarNombreLibreAsync(entrenador.NombreCompleto, id, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = Roles.Gestion)]
        [HttpPut("{id:long}/estado")]
        public async Task<IActionResult> CambiarEstado(long id, [FromBody] CambiarEstadoRequest request, CancellationToken cancellationToken)
        {
            var entrenador = await BuscarAsync(id, cancellationToken);
            if (request.Activo)
            {
                entrenador.Activar();
            }
            else
            {
                var horas = await _context.TurnosEncargado.CountAsync(t => t.EntrenadorId == id, cancellationToken);
                if (horas > 0)
                    throw new InvalidOperationException(
                        $"{entrenador.NombreCompleto} tiene {horas} {(horas == 1 ? "hora" : "horas")} en el calendario por semana. Liberalas antes de desactivarlo.");
                entrenador.Desactivar();
            }

            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        // ---------- foto ----------

        /// <summary>
        /// La foto de la ficha: cualquier usuario con sesión. Se manda siempre como image/jpeg, sin que el navegador adivine otro tipo,
        /// y con un ETag (la fecha de la última subida): el navegador la guarda pero pregunta antes de usarla, y si no cambió el
        /// servidor contesta 304 sin mandarla de nuevo.
        /// </summary>
        [HttpGet("{id:long}/foto")]
        public async Task<IActionResult> VerFoto(long id, CancellationToken cancellationToken)
        {
            var foto = await _context.FotosDeEntrenador.AsNoTracking().FirstOrDefaultAsync(f => f.EntrenadorId == id, cancellationToken);
            if (foto is null) return NotFound();

            Response.Headers.CacheControl = "private, no-cache";
            Response.Headers.XContentTypeOptions = "nosniff";
            return File(foto.Foto, "image/jpeg", foto.Actualizada, new EntityTagHeaderValue($"\"{foto.Actualizada.UtcTicks}\""));
        }

        /// <summary>
        /// Sube o reemplaza la foto. El cuerpo es el JPEG tal cual (la pantalla ya lo achicó a 256 × 256). Se valida la estructura del
        /// archivo, no el tipo que declare quien lo manda.
        /// </summary>
        [Authorize(Roles = Roles.Gestion)]
        [HttpPut("{id:long}/foto")]
        [RequestSizeLimit(TamanoMaximoDelPedidoDeFoto)]
        public async Task<IActionResult> SubirFoto(long id, CancellationToken cancellationToken)
        {
            await BuscarAsync(id, cancellationToken);
            var bytes = await LeerElCuerpoAsync(cancellationToken);
            ReglasDeFoto.Validar(bytes);

            var ahora = _reloj.GetUtcNow();
            var existente = await _context.FotosDeEntrenador.FirstOrDefaultAsync(f => f.EntrenadorId == id, cancellationToken);
            if (existente is null) _context.FotosDeEntrenador.Add(new FotoDeEntrenador(id, bytes, ahora));
            else existente.Reemplazar(bytes, ahora);
            await _context.SaveChangesAsync(cancellationToken);

            await _eventos.RegistrarAsync("entrenador", "FOTO", _usuario.Email ?? "sistema", cancellationToken, id.ToString());
            return NoContent();
        }

        /// <summary>Quita la foto. Si no tenía, no pasa nada (el resultado es el mismo: la ficha sin foto).</summary>
        [Authorize(Roles = Roles.Gestion)]
        [HttpDelete("{id:long}/foto")]
        public async Task<IActionResult> QuitarFoto(long id, CancellationToken cancellationToken)
        {
            await BuscarAsync(id, cancellationToken);
            var quitadas = await _context.FotosDeEntrenador.Where(f => f.EntrenadorId == id).ExecuteDeleteAsync(cancellationToken);
            if (quitadas > 0)
                await _eventos.RegistrarAsync("entrenador", "FOTO_BAJA", _usuario.Email ?? "sistema", cancellationToken, id.ToString());
            return NoContent();
        }

        /// <summary>Lee el cuerpo del pedido, y deja de leer cuando ya pasó el máximo de una foto: lo que sobra no se guarda en memoria.</summary>
        private async Task<byte[]> LeerElCuerpoAsync(CancellationToken cancellationToken)
        {
            using var memoria = new MemoryStream();
            var buffer = new byte[16 * 1024];
            int leidos;
            while ((leidos = await Request.Body.ReadAsync(buffer, cancellationToken)) > 0)
            {
                memoria.Write(buffer, 0, leidos);
                if (memoria.Length > ReglasDeFoto.BytesMaximos) break;   // ya no entra: Validar la rechaza con su mensaje
            }
            return memoria.ToArray();
        }

        private async Task<Entrenador> BuscarAsync(long id, CancellationToken cancellationToken)
            => await _context.Entrenadores.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
               ?? throw new NotFoundException(nameof(Entrenador), id);

        /// <summary>
        /// El nombre no se repite aunque cambien las mayúsculas (el nombre ya viene sin espacios repetidos). Se compara acá, y no
        /// solo en la base, porque lower() de PostgreSQL depende de la configuración regional y no siempre pliega los acentos;
        /// el índice único ux_entrenadores_nombre queda de respaldo.
        /// </summary>
        private async Task ValidarNombreLibreAsync(string nombre, long? idExcluido, CancellationToken cancellationToken)
        {
            var existentes = await _context.Entrenadores.AsNoTracking()
                .Select(e => new { e.Id, e.NombreCompleto })
                .ToListAsync(cancellationToken);
            if (existentes.Any(e => e.Id != idExcluido && string.Equals(e.NombreCompleto, nombre, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Ya hay un entrenador con ese nombre.");
        }
    }
}
