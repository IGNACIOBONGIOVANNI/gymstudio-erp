using System.Text.Json;
using EstudioContable.Application.Common.Exceptions;
using EstudioContable.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace EstudioContable.API.Middlewares
{
    /// <summary>
    /// Middleware global: intercepta cualquier excepción no controlada y la traduce
    /// a una respuesta JSON consistente (RFC 7807 - ProblemDetails) para que el
    /// frontend siempre reciba la misma forma de error, sin importar la capa que
    /// lo haya lanzado (dominio, validación, o un error inesperado).
    ///
    /// Mapeo de excepciones -> HTTP status:
    ///   AsientoDesbalanceadoException -> 422 Unprocessable Entity (regla de negocio violada)
    ///   NotFoundException             -> 404 Not Found
    ///   FluentValidation.ValidationException -> 400 Bad Request (errores por campo)
    ///   InvalidOperationException     -> 409 Conflict (estado inválido para la operación)
    ///   UnauthorizedAccessException   -> 403 Forbidden
    ///   Cualquier otra excepción      -> 500 Internal Server Error (detalle oculto en producción)
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IHostEnvironment _env;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger,
            IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var (statusCode, problem) = exception switch
            {
                AsientoDesbalanceadoException ex => (
                    StatusCodes.Status422UnprocessableEntity,
                    BuildProblem(
                        context, StatusCodes.Status422UnprocessableEntity,
                        title: "El asiento no está balanceado",
                        detail: ex.Message,
                        type: "asiento-desbalanceado",
                        extensions: new Dictionary<string, object?>
                        {
                            ["totalDebe"] = ex.TotalDebe,
                            ["totalHaber"] = ex.TotalHaber,
                            ["diferencia"] = ex.Diferencia
                        })
                ),

                NotFoundException ex => (
                    StatusCodes.Status404NotFound,
                    BuildProblem(context, StatusCodes.Status404NotFound,
                        title: "Recurso no encontrado",
                        detail: ex.Message,
                        type: "not-found")
                ),

                ValidationException ex => (
                    StatusCodes.Status400BadRequest,
                    BuildProblem(context, StatusCodes.Status400BadRequest,
                        title: "Uno o más campos no son válidos",
                        detail: "Revisá los datos enviados e intentá nuevamente.",
                        type: "validation-error",
                        extensions: new Dictionary<string, object?>
                        {
                            ["errores"] = ex.Errors
                                .GroupBy(e => e.PropertyName)
                                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
                        })
                ),

                InvalidOperationException ex => (
                    StatusCodes.Status409Conflict,
                    BuildProblem(context, StatusCodes.Status409Conflict,
                        title: "Operación inválida en el estado actual",
                        detail: ex.Message,
                        type: "invalid-operation")
                ),

                UnauthorizedAccessException ex => (
                    StatusCodes.Status403Forbidden,
                    BuildProblem(context, StatusCodes.Status403Forbidden,
                        title: "Acceso denegado",
                        detail: ex.Message,
                        type: "forbidden")
                ),

                _ => (
                    StatusCodes.Status500InternalServerError,
                    BuildProblem(context, StatusCodes.Status500InternalServerError,
                        title: "Error interno del servidor",
                        // En producción NUNCA se expone el stack trace ni el mensaje interno.
                        detail: _env.IsDevelopment()
                            ? exception.ToString()
                            : "Ocurrió un error inesperado. Contactá al soporte.",
                        type: "internal-error")
                )
            };

            if (statusCode == StatusCodes.Status500InternalServerError)
                _logger.LogError(exception, "Error no controlado en {Path}", context.Request.Path);
            else
                _logger.LogWarning(exception, "Excepción controlada ({StatusCode}) en {Path}", statusCode, context.Request.Path);

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
        }

        private static ProblemDetails BuildProblem(
            HttpContext context,
            int statusCode,
            string title,
            string? detail,
            string type,
            Dictionary<string, object?>? extensions = null)
        {
            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                // Namespace propio de errores de negocio, útil para que el frontend
                // pueda hacer switch por tipo en vez de parsear el mensaje en texto libre.
                Type = $"https://estudiocontable.api/errores/{type}",
                Instance = context.Request.Path
            };

            if (extensions is not null)
            {
                foreach (var (key, value) in extensions)
                    problem.Extensions[key] = value;
            }

            return problem;
        }
    }

    /// <summary>Extensión para registrar el middleware de forma legible en Program.cs.</summary>
    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
            => app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
