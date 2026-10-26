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
