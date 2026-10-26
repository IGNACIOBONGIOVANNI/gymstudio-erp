using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using EstudioContable.Infrastructure.Persistence;
using EstudioContable.Domain.Interfaces;
using EstudioContable.Infrastructure.Persistence.Repositories;
using EstudioContable.Application.Common.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Conexión a Base de Datos
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers().AddJsonOptions(x =>
    x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

// Configuración de la política CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirFrontend", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Registro de Repositorios, Servicios y UnitOfWork
builder.Services.AddScoped<IAsientoRepository, AsientoRepository>();
builder.Services.AddScoped<ICuentaRepository, CuentaRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<EstudioContable.Application.Interfaces.IFacturacionService, EstudioContable.Application.Services.FacturacionService>();
builder.Services.AddScoped<EstudioContable.Application.Interfaces.IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

// Registro de MediatR (apuntando al Assembly de la capa Application)
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(EstudioContable.Application.Asientos.Commands.CrearAsiento.CrearAsientoCommand).Assembly));

// ¡Acá habilitamos la creación de la interfaz Swagger!
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => 
{
    // Esto obliga a Swagger a usar la ruta completa de las clases para no confundirse y arregla el error 500
    c.CustomSchemaIds(type => type.FullName);
});

var app = builder.Build();

app.UseMiddleware<EstudioContable.API.Middlewares.ExceptionMiddleware>();

// ¡Acá encendemos el Middleware de CORS! (Debe ir antes del mapeo de controladores)
app.UseCors("PermitirFrontend");

// ¡Acá encendemos Swagger para poder usarlo!
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

// Aplicar migraciones pendientes automáticamente en la base de datos
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrió un error al aplicar las migraciones de la base de datos.");
    }
}

app.Run();