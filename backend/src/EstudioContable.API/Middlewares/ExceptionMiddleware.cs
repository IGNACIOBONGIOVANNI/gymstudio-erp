using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EstudioContable.API.Middlewares
{
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
                await HandleExceptionAsync(context, ex, HttpStatusCode.NotFound);
            }
            catch (EstudioContable.Domain.Exceptions.AsientoDesbalanceadoException ex)
            {
                _logger.LogWarning(ex, "Unbalanced journal entry");
                await HandleExceptionAsync(context, ex, HttpStatusCode.UnprocessableEntity);
            }
            catch (Exception ex) when (ObtenerErrorDeRegla(ex) is Npgsql.PostgresException pg)
            {
                // Un trigger o un CHECK de PostgreSQL rechazó la operación: es una regla
                // de negocio, no un error del servidor. Se informa el mensaje de la base.
                _logger.LogWarning(ex, "Database rule violation {SqlState}", pg.SqlState);
                await HandleExceptionAsync(context, new InvalidOperationException(pg.MessageText), HttpStatusCode.BadRequest);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized");
                await HandleExceptionAsync(context, ex, HttpStatusCode.Unauthorized);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Domain validation error");
                await HandleExceptionAsync(context, ex, HttpStatusCode.BadRequest);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Argument validation error");
                await HandleExceptionAsync(context, ex, HttpStatusCode.BadRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception");
                await HandleExceptionAsync(context, ex, HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Devuelve la PostgresException si el error es una violación de regla de la base:
        /// RAISE EXCEPTION de un trigger (P0001) o restricciones de integridad (clase 23).
        /// </summary>
        private static Npgsql.PostgresException? ObtenerErrorDeRegla(Exception ex)
        {
            for (var actual = ex; actual != null; actual = actual.InnerException)
            {
                if (actual is Npgsql.PostgresException pg && (pg.SqlState == "P0001" || pg.SqlState.StartsWith("23")))
                    return pg;
            }
            return null;
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception, HttpStatusCode statusCode)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var problemDetails = new ProblemDetails
            {
                Status = (int)statusCode,
                Title = statusCode switch
                {
                    HttpStatusCode.BadRequest => "Error de Validación",
                    HttpStatusCode.NotFound => "Recurso no encontrado",
                    HttpStatusCode.Unauthorized => "No autenticado",
                    HttpStatusCode.UnprocessableEntity => "Asiento desbalanceado",
                    _ => "Error Interno del Servidor"
                },
                Detail = exception.Message
            };

            var json = JsonSerializer.Serialize(problemDetails);
            return context.Response.WriteAsync(json);
        }
    }
}
