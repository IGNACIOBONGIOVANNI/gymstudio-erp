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

// Conexión a Base de Datos. El interceptor informa a PostgreSQL qué usuario está
// logueado, para que el trigger de auditoría lo registre.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<EstudioContable.Application.Interfaces.IUsuarioActual, UsuarioActualHttp>();
builder.Services.AddScoped<AuditoriaConnectionInterceptor>();
builder.Services.AddDbContext<AppDbContext>((services, options) =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .AddInterceptors(services.GetRequiredService<AuditoriaConnectionInterceptor>()));

// IP real del equipo. Detrás del proxy del frontend (Vite en Docker, o un Nginx/Caddy en producción) la
// conexión llega desde el proxy: se toma la IP de X-Forwarded-For, pero SOLO si el pedido viene de una red
// de confianza (por defecto, redes privadas y la propia máquina). Si no, cualquiera podría escribir el
// encabezado a mano para falsear su IP en la auditoría y esquivar el límite de intentos de login.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(opciones =>
{
    opciones.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor;
    opciones.ForwardLimit = 1;   // se toma la dirección que agregó el proxy más cercano, no las anteriores
    opciones.KnownIPNetworks.Clear();
    opciones.KnownProxies.Clear();
    var redes = builder.Configuration.GetSection("Proxy:RedesDeConfianza").Get<string[]>();
    if (redes == null || redes.Length == 0)
        redes = new[] { "127.0.0.0/8", "::1/128", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16" };
    foreach (var red in redes)
        if (System.Net.IPNetwork.TryParse(red, out var parseada))
            opciones.KnownIPNetworks.Add(parseada);
});

builder.Services.AddControllers()
    .AddJsonOptions(x => x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles)
    .ConfigureApiBehaviorOptions(options =>
    {
        // Datos con formato inválido (letras en un número, fecha mal escrita, campo
        // faltante): respuesta en castellano con el nombre del campo, en lugar del
        // mensaje técnico de System.Text.Json.
        options.InvalidModelStateResponseFactory = context =>
        {
            var conError = context.ModelState.Where(e => e.Value?.Errors.Count > 0).ToList();

            // "$" = el cuerpo no es un JSON válido; "$.campo" = un campo con tipo incorrecto.
            var jsonRoto = conError.Any(e => e.Key == "$");
            var camposJson = conError.Where(e => e.Key.StartsWith("$.")).Select(e => e.Key[2..]);
            // Parámetros de la URL con valor inválido (ej. ?fechaInicio=ayer).
            var parametros = conError
                .Where(e => !e.Key.StartsWith("$") && e.Key.Length > 0
                            && !e.Value!.Errors.Any(err => err.ErrorMessage.Contains("required")))
                .Select(e => e.Key);
            var invalidos = camposJson.Concat(parametros).Distinct().ToList();

            string detalle;
            if (jsonRoto)
                detalle = "Los datos enviados no tienen un formato válido.";
            else if (invalidos.Count > 0)
                detalle = $"Hay datos con formato inválido en: {string.Join(", ", invalidos)}. Revisá que los números y las fechas estén bien escritos.";
            else
                detalle = $"Faltan datos obligatorios: {string.Join(", ", conError.Select(e => e.Key).Where(k => k.Length > 0))}.";

            return new BadRequestObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error de Validación",
                Detail = detalle
            });
        };

        // Respuestas NotFound() sin cuerpo: título en castellano para mostrar en pantalla.
        options.ClientErrorMapping[StatusCodes.Status404NotFound].Title = "No se encontró el registro solicitado.";
    });

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
// La clave que firma las sesiones no está en el repositorio: se configura (Jwt__Key) o cada instalación genera la suya la primera
// vez y la guarda en la carpeta .secretos, que no se sube al control de versiones.
ClaveDeSesion.Resolver(builder.Configuration, builder.Configuration["Jwt:CarpetaDeLaClave"] ?? Path.Combine(builder.Environment.ContentRootPath, ".secretos"));
var jwt = builder.Configuration.GetSection(JwtOptions.Seccion).Get<JwtOptions>() ?? new JwtOptions();

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
            // Un token firmado no alcanza: el usuario tiene que seguir activo y con el mismo
            // rol que figura en el token. Así una baja o un cambio de rol rige al instante
            // (y no recién cuando vence el token).
            OnTokenValidated = async context =>
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                long id;
                try { id = TokenService.ObtenerUsuarioId(context.Principal!); }
                catch (UnauthorizedAccessException) { context.Fail("Token sin usuario."); return; }

                var actual = await db.Usuarios.AsNoTracking()
                    .Where(u => u.Id == id)
                    .Select(u => new { u.Activo, u.Rol })
                    .FirstOrDefaultAsync(context.HttpContext.RequestAborted);
                var rolDelToken = context.Principal!.FindFirst("role")?.Value;

                if (actual == null || !actual.Activo || actual.Rol.ToString() != rolDelToken)
                    context.Fail("La sesión ya no es válida.");
            },
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
builder.Services.AddScoped<EstudioContable.Application.Services.ITesoreriaService, EstudioContable.Application.Services.TesoreriaService>();
builder.Services.AddScoped<EstudioContable.Application.Services.IControlIntegridadService, EstudioContable.Application.Services.ControlIntegridadService>();
builder.Services.AddScoped<EstudioContable.Application.Services.ICuentasCorrientesService, EstudioContable.Application.Services.CuentasCorrientesService>();
builder.Services.AddScoped<EstudioContable.Application.Services.IAsistenteService, EstudioContable.Application.Services.AsistenteService>();
builder.Services.AddScoped<EstudioContable.Application.Services.IAsistenciasService, EstudioContable.Application.Services.AsistenciasService>();
builder.Services.AddScoped<EstudioContable.Application.Services.ITableroDeGestionService, EstudioContable.Application.Services.TableroDeGestionService>();
builder.Services.AddScoped<EstudioContable.Application.Interfaces.IRegistroDeEventos, EstudioContable.Infrastructure.Persistence.RegistroDeEventos>();
builder.Services.AddScoped<EstudioContable.Application.Copias.ICopiasDeSeguridadService, EstudioContable.Infrastructure.Copias.CopiasDeSeguridadService>();
// El reloj del sistema (se inyecta para poder probar las copias automáticas fijando la hora) y el servicio de copias automáticas.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<EstudioContable.Application.Copias.ICopiasAutomaticasService, EstudioContable.Infrastructure.Copias.CopiasAutomaticasService>();
// Horas cumplidas de los entrenadores y presupuesto de pago (solo gestión; usa el mismo reloj para el «hoy» de Argentina).
builder.Services.AddScoped<EstudioContable.Application.Entrenadores.IHorasDeEntrenadoresService, EstudioContable.Infrastructure.Entrenadores.HorasDeEntrenadoresService>();
// La tarea que hace sola las copias programadas. Se apaga con Copias:TareaAutomatica = false (las pruebas lo hacen).
if (builder.Configuration.GetValue("Copias:TareaAutomatica", true))
    builder.Services.AddHostedService<EstudioContable.API.Copias.CopiasAutomaticasHostedService>();
builder.Services.AddScoped<EstudioContable.Application.Interfaces.IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

// Registro de MediatR (apuntando al Assembly de la capa Application)
var assemblyAplicacion = typeof(EstudioContable.Application.Asientos.Commands.CrearAsiento.CrearAsientoCommand).Assembly;
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(assemblyAplicacion);
    // Corre los validadores de FluentValidation antes de cada handler.
    cfg.AddOpenBehavior(typeof(EstudioContable.Application.Common.Behaviors.ValidationBehavior<,>));
});

// Registra todos los validadores (AbstractValidator<T>) de la capa Application.
foreach (var tipo in assemblyAplicacion.GetTypes().Where(t => t is { IsAbstract: false, IsGenericTypeDefinition: false }))
{
    foreach (var interfaz in tipo.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(FluentValidation.IValidator<>)))
        builder.Services.AddScoped(interfaz, tipo);
}

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

// Primero de todo: el límite de login y la auditoría usan la IP ya corregida.
app.UseForwardedHeaders();

app.UseMiddleware<EstudioContable.API.Middlewares.ExceptionMiddleware>();

// Cabeceras de seguridad: sin adivinar tipos de archivo, sin incrustar la API en otra página,
// sin filtrar la URL de origen y sin guardar en caché datos contables ni tokens.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var h = context.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "no-referrer";
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            // Por defecto nada se guarda en caché. Un endpoint que lo dice explícitamente (hoy, solo la foto del entrenador:
            // «private, no-cache», que el navegador guarda pero revalida con el ETag antes de usar) conserva lo suyo.
            if (!h.ContainsKey("Cache-Control")) h["Cache-Control"] = "no-store";
            h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        }
        return Task.CompletedTask;
    });
    await next();
});

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
        await EstudioContable.API.Datos.RutinasIniciales.CrearSiNoExistenAsync(context);
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

// Visible para los tests de integración (WebApplicationFactory<Program>).
public partial class Program { }
