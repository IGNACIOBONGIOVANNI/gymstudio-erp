using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using EstudioContable.API.Auth;
using EstudioContable.API.Controllers;
using EstudioContable.Application.Common.Interfaces;
using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Interfaces;
using EstudioContable.Infrastructure.Persistence;
using EstudioContable.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Conexión a Base de Datos
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers().AddJsonOptions(x =>
    x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

// CORS: sólo los orígenes del frontend configurados en "Cors:OrigenesPermitidos".
var origenesPermitidos = builder.Configuration.GetSection("Cors:OrigenesPermitidos").Get<string[]>()
    ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirFrontend", policy =>
    {
        policy.WithOrigins(origenesPermitidos)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Autenticación JWT
var jwt = builder.Configuration.GetSection(JwtOptions.Seccion).Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32)
    throw new InvalidOperationException("Falta configurar Jwt:Key (mínimo 32 caracteres) en appsettings o en la variable de entorno Jwt__Key.");

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Seccion));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Conserva los nombres cortos de los claims ("sub", "role", "name").
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = TokenService.CrearClave(jwt.Key),
            NameClaimType = "name",
            RoleClaimType = "role"
        };

        // Respuestas 401/403 con cuerpo JSON, para que el frontend muestre el motivo.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await EscribirProblema(context.Response, StatusCodes.Status401Unauthorized,
                    "No autenticado", "Tu sesión expiró o no iniciaste sesión.");
            },
            OnForbidden = context => EscribirProblema(context.Response, StatusCodes.Status403Forbidden,
                "Sin permisos", "Tu usuario no tiene permisos para realizar esta acción.")
        };
    });

// Toda la API exige un usuario autenticado salvo lo marcado con [AllowAnonymous] (el login).
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

// Límite de intentos de login por IP, contra ataques de fuerza bruta.
var intentosLoginPorMinuto = builder.Configuration.GetValue("Auth:IntentosLoginPorMinuto", 10);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthController.PoliticaLimiteLogin, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = intentosLoginPorMinuto, Window = TimeSpan.FromMinutes(1) }));
    options.OnRejected = (context, _) => new ValueTask(EscribirProblema(context.HttpContext.Response,
        StatusCodes.Status429TooManyRequests, "Demasiados intentos", "Demasiados intentos de inicio de sesión. Esperá un minuto."));
});

// Registro de Repositorios, Servicios y UnitOfWork
builder.Services.AddScoped<IAsientoRepository, AsientoRepository>();
builder.Services.AddScoped<ICuentaRepository, CuentaRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<EstudioContable.Application.Interfaces.IFacturacionService, EstudioContable.Application.Services.FacturacionService>();
builder.Services.AddScoped<EstudioContable.Application.Interfaces.IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

// Registro de MediatR (apuntando al Assembly de la capa Application)
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(EstudioContable.Application.Asientos.Commands.CrearAsiento.CrearAsientoCommand).Assembly));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Esto obliga a Swagger a usar la ruta completa de las clases para no confundirse y arregla el error 500
    c.CustomSchemaIds(type => type.FullName);

    // Botón "Authorize" en Swagger para pegar el token devuelto por /api/auth/login.
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Pegá el token obtenido en POST /api/auth/login"
    });
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});

var app = builder.Build();

app.UseMiddleware<EstudioContable.API.Middlewares.ExceptionMiddleware>();

app.UseCors("PermitirFrontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

// Aplicar migraciones pendientes y crear los usuarios iniciales si no hay ninguno
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        context.Database.Migrate();
        await UsuariosIniciales.CrearSiNoExistenAsync(
            context, services.GetRequiredService<IPasswordHasher<Usuario>>(), app.Configuration);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrió un error al preparar la base de datos.");
    }
}

app.Run();

static Task EscribirProblema(HttpResponse response, int status, string titulo, string detalle)
{
    response.StatusCode = status;
    response.ContentType = "application/problem+json";
    return response.WriteAsync(JsonSerializer.Serialize(new ProblemDetails { Status = status, Title = titulo, Detail = detalle }));
}
