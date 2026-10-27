using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EstudioContable.API.Middlewares
{
    /// <summary>
    /// Traduce las excepciones a respuestas HTTP con un mensaje en castellano que el
    /// frontend puede mostrar tal cual. Nunca expone detalles internos en un 500.
    /// </summary>
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (EstudioContable.Application.Common.Exceptions.NotFoundException ex)
            {
                _logger.LogWarning(ex, "Resource not found");
                await Responder(context, HttpStatusCode.NotFound, "Recurso no encontrado", ex.Message);
            }
            catch (EstudioContable.Domain.Exceptions.AsientoDesbalanceadoException ex)
            {
                _logger.LogWarning(ex, "Unbalanced journal entry");
                await Responder(context, HttpStatusCode.UnprocessableEntity, "Asiento desbalanceado", ex.Message);
            }
            catch (FluentValidation.ValidationException ex)
            {
                _logger.LogWarning(ex, "Validation error");
                var detalle = string.Join(" ", ex.Errors.Select(e => e.ErrorMessage).Distinct());
                await Responder(context, HttpStatusCode.BadRequest, "Error de Validación", detalle);
            }
            catch (Exception ex) when (ObtenerErrorDeBase(ex) is Npgsql.PostgresException pg)
            {
                // Un trigger, un CHECK o una clave foránea de PostgreSQL rechazó la operación:
                // es una regla de negocio, no un error del servidor.
                _logger.LogWarning(ex, "Database rule violation {SqlState}", pg.SqlState);
                await Responder(context, HttpStatusCode.BadRequest, "Error de Validación", TraducirErrorDeBase(pg));
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized");
                await Responder(context, HttpStatusCode.Unauthorized, "No autenticado", ex.Message);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Argument validation error");
                await Responder(context, HttpStatusCode.BadRequest, "Error de Validación", SinNombreDeParametro(ex));
            }
            catch (InvalidOperationException ex) when (ex.InnerException == null)
            {
                // Reglas de negocio del dominio (ej. "stock insuficiente", "ya está anulada").
                _logger.LogWarning(ex, "Domain validation error");
                await Responder(context, HttpStatusCode.BadRequest, "Error de Validación", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception");
                await Responder(context, HttpStatusCode.InternalServerError, "Error Interno del Servidor",
                    "Ocurrió un error inesperado. Si el problema persiste, revisá que la base de datos esté disponible.");
            }
        }

        /// <summary>
        /// PostgresException si el error es una regla de la base: RAISE EXCEPTION de un
        /// trigger (P0001), integridad (clase 23) o un número fuera de rango (22003).
        /// </summary>
        private static Npgsql.PostgresException? ObtenerErrorDeBase(Exception ex)
        {
            for (var actual = ex; actual != null; actual = actual.InnerException)
            {
                if (actual is Npgsql.PostgresException pg
                    && (pg.SqlState == "P0001" || pg.SqlState == "22003" || pg.SqlState.StartsWith("23")))
                    return pg;
            }
            return null;
        }

        private static string TraducirErrorDeBase(Npgsql.PostgresException pg) => pg.SqlState switch
        {
            // Mensajes propios de los triggers: ya están en castellano.
            "P0001" => pg.MessageText,
            "22003" => "Uno de los importes es demasiado grande.",
            "23503" when pg.MessageText.StartsWith("update or delete")
                => "No se puede eliminar porque hay otros registros que lo usan (facturas, movimientos o membresías).",
            "23503" => "Uno de los datos hace referencia a un registro que no existe (por ejemplo, una cuenta o un producto).",
            "23505" when pg.ConstraintName == "UX_Facturas_NumeroVenta"
                => "Otra venta tomó ese número de comprobante al mismo tiempo. Volvé a guardar.",
            "23505" when pg.ConstraintName == "UX_Facturas_NumeroCompra"
                => "Esa factura del proveedor ya está cargada.",
            "23505" => "Ya existe un registro con esos datos.",
            "23514" => $"Los datos no cumplen una regla de la base de datos ({pg.ConstraintName}).",
            _ => "La base de datos rechazó la operación."
        };

        // "El nombre es obligatorio. (Parameter 'nombre')" -> "El nombre es obligatorio."
        private static string SinNombreDeParametro(ArgumentException ex)
            => ex.ParamName == null ? ex.Message : ex.Message.Replace($" (Parameter '{ex.ParamName}')", "");

        private static Task Responder(HttpContext context, HttpStatusCode status, string titulo, string detalle)
        {
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = (int)status;
            var problema = new ProblemDetails { Status = (int)status, Title = titulo, Detail = detalle };
            return context.Response.WriteAsync(JsonSerializer.Serialize(problema));
        }
    }
}
