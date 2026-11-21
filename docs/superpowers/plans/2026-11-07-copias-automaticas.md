# Copias de seguridad automáticas · Plan de implementación (Parte 2 de 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que el Administrador programe desde *Configuración › Copia de seguridad* una copia automática (cada 6 h, cada 12 h, diaria, semanal o mensual) y que el sistema la haga solo, la guarde firmada en `backups/automaticas` y conserve las últimas N.

**Architecture:** Una fila de configuración en la base (`ConfiguracionDeCopias`), una regla de horario pura en el dominio, un servicio que genera la copia con el `ICopiasDeSeguridadService` existente y la escribe en una carpeta, y un `BackgroundService` que cada minuto le pregunta si toca. API nueva bajo `api/copias` solo para el Administrador y un bloque nuevo en la pantalla.

**Tech Stack:** .NET 10 / EF Core / PostgreSQL 15 (xUnit + Testcontainers), React 19 + TypeScript + Tailwind (pruebas con `node --test`).

**Spec:** `docs/superpowers/specs/2026-11-07-entrenadores-y-copias-automaticas-design.md` (sección «Parte 2»). Se hace después de la Parte 1 (`2026-11-07-entrenadores-en-el-calendario.md`), ya commiteada.

## Global Constraints

- Frecuencias: `CadaSeisHoras`, `CadaDoceHoras`, `Diaria`, `Semanal`, `Mensual`. `Hora` 0–23, `Minuto` 0–59 (por defecto 22:00), `DiaDeLaSemana` 1 (lunes) a 7 (domingo) solo si es semanal, `DiaDelMes` 1 a **28** solo si es mensual, `Conservar` 1 a 60 (por defecto 14), `Activa` por defecto `false`.
- Todas las horas se interpretan en **Argentina (UTC-3, sin horario de verano)**, igual que el resto del sistema (`UtcNow.AddHours(-3)`). El reloj se inyecta (`TimeProvider`).
- Reintento tras una falla: **30 minutos**. La tarea revisa **cada minuto**.
- Archivos: `gymstudio_auto_AAAAMMDD_HHMMSS.zip` (hora argentina), validados siempre con `^gymstudio_auto_\d{8}_\d{6}\.zip\z` (`\z`, no `$`); temporal `…zip.tmp`. La retención y la descarga solo tocan nombres que cumplen esa expresión.
- Carpeta: configuración `Copias:Carpeta` (por defecto `backups/automaticas`; en Docker `/backups/automaticas`). Tarea en segundo plano: `Copias:TareaAutomatica` (por defecto `true`; las pruebas la apagan).
- Tabla `contabilidad."ConfiguracionDeCopias"`: una sola fila (`Id = 1`, con `CHECK`), con las restricciones también en la base.
- Solo `Roles.Administrador` ve, cambia y descarga. Formato de la copia: el firmado de hoy, sin un segundo formato. Siguen sin estar cifradas.
- Auditoría (tabla `copia`): `AUTOMATICA` y `FALLIDA` con usuario «sistema», `GUARDADA` (copia hecha «ahora») y `PROGRAMACION` con el email de quien actuó.
- Textos en castellano rioplatense (voseo), sin emojis; errores de validación junto a cada campo. Pruebas de integración necesitan Docker; `npm test` se corre desde PowerShell (falla en Git Bash).
- **Docker del usuario:** igual que en la Parte 1 (`docker compose stop backend frontend` antes de empezar; `scripts\backup.ps1` y `docker compose up -d` al final). El cambio de `docker-compose.yml` exige recrear el contenedor.
- Desvío menor del spec: la referencia para calcular la próxima copia es `max(UltimaCopia, ActivaDesde)`, así reprogramar no dispara una copia atrasada de inmediato.

## Review Focus

- La carpeta no se puede escribir (permisos o disco lleno): el error se guarda y se muestra, el servicio sigue vivo y reintenta a los 30 minutos, no cada minuto (Task 3).
- El sistema estuvo apagado varios períodos: al volver se hace **una** copia y no se repite en la vuelta siguiente (Task 3).
- Nombres de descarga hostiles (`..\`, `../`, `%2e%2e`, absolutos, con salto de línea final, `.tmp`) nunca salen de la carpeta ni abren otro archivo (Tasks 1, 3 y 4).
- Programaciones imposibles (semanal sin día, mensual el 29 al 31, hora 24, conservar 0 o 1000, frecuencia `"99"`) se rechazan con 400 y no cambian lo guardado (Tasks 3 y 4).
- La retención no borra archivos ajenos en la carpeta (`notas.zip`, `gymstudio_auto_x.zip`, volcados `.dump`, subcarpetas), y «Hacer una copia ahora» a la vez que la tarea no deja archivos rotos (Task 3).

---

## Task 1: Regla de horario y configuración (dominio)

**Files:**
- Create: `backend/src/EstudioContable.Domain/Enums/FrecuenciaDeCopia.cs`, `backend/src/EstudioContable.Domain/Entities/ConfiguracionDeCopias.cs`, `backend/src/EstudioContable.Domain/ReglasDeCopiasAutomaticas.cs`
- Test: `backend/tests/EstudioContable.Domain.Tests/ReglasDeCopiasAutomaticasTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public enum FrecuenciaDeCopia { CadaSeisHoras = 1, CadaDoceHoras = 2, Diaria = 3, Semanal = 4, Mensual = 5 }

  public class ConfiguracionDeCopias {
      public const int IdUnico = 1;
      public int Id { get; private set; } = IdUnico;
      public bool Activa { get; private set; }
      public FrecuenciaDeCopia Frecuencia { get; private set; } = FrecuenciaDeCopia.Diaria;
      public int Hora { get; private set; } = 22;
      public int Minuto { get; private set; }
      public int? DiaDeLaSemana { get; private set; }
      public int? DiaDelMes { get; private set; }
      public int Conservar { get; private set; } = 14;
      public DateTimeOffset ActivaDesde { get; private set; }
      public DateTimeOffset? UltimaCopia { get; private set; }
      public DateTimeOffset? UltimoIntento { get; private set; }
      public string? UltimoError { get; private set; }
      public void Programar(bool activa, FrecuenciaDeCopia frecuencia, int hora, int minuto, int? diaDeLaSemana, int? diaDelMes, int conservar, DateTimeOffset ahora);
      public void RegistrarCopia(DateTimeOffset ahora);                 // UltimaCopia = UltimoIntento = ahora; UltimoError = null
      public void RegistrarFalla(DateTimeOffset ahora, string mensaje); // UltimoIntento = ahora; UltimoError = mensaje
  }

  public static class ReglasDeCopiasAutomaticas {
      public static readonly TimeSpan DesfaseArgentina = TimeSpan.FromHours(-3);
      public static readonly TimeSpan EsperaTrasUnaFalla = TimeSpan.FromMinutes(30);
      public static DateTimeOffset ProximaEjecucion(ConfiguracionDeCopias config, DateTimeOffset referencia); // primer momento programado estrictamente posterior; devuelve offset -03:00
      public static DateTimeOffset? ProximaCopia(ConfiguracionDeCopias config);  // null si no está activa; ProximaEjecucion(config, max(UltimaCopia ?? ActivaDesde, ActivaDesde))
      public static bool Toca(ConfiguracionDeCopias config, DateTimeOffset ahora);
      public static string NombreDeArchivo(DateTimeOffset ahora);
      public static bool EsNombreDeCopiaAutomatica(string? nombre);
  }
  ```
  `Programar` valida con `ReglasDeDatos.Entero`/`ArgumentException`: hora, minuto y conservar en rango; semanal exige `diaDeLaSemana` 1–7; mensual exige `diaDelMes` 1–28; en las demás frecuencias guarda ambos días como `null`; fija `ActivaDesde = ahora`.

- [ ] **Step 1: Escribir `ReglasDeCopiasAutomaticasTests` (fallan por compilación).** Fechas con offset `-03:00`; `Fecha(2026, 11, 7, 21, 59)` = `new DateTimeOffset(2026,11,7,21,59,0, TimeSpan.FromHours(-3))`. `ProximaEjecucion`: diaria 22:00 con referencia 7/11 21:59 → 7/11 22:00; con referencia 7/11 22:00 → 8/11 22:00 (estrictamente posterior); semanal lunes 08:30 con referencia viernes 6/11 10:00 → lunes 9/11 08:30 y con referencia lunes 9/11 08:30 → 16/11 08:30; mensual día 28 22:00 con referencia 28/2/2026 22:00 → 28/3/2026 22:00 y con 28/12/2026 22:01 → 28/1/2027 22:00; `CadaSeisHoras` 02:00 con referencia 7/11 02:00 → 08:00, con 7/11 20:00 → 8/11 02:00; `CadaDoceHoras` 06:30 con referencia 06:30 → 18:30 y con 18:30 → 8/11 06:30. `Toca`: inactiva → `false`; diaria 22:00, `ActivaDesde` 6/11 23:00, sin copia: 7/11 21:59 → `false`, 22:00 → `true`; `UltimaCopia` hace tres días a las 22:00 y ahora 7/11 23:00 → `true`, y tras `RegistrarCopia(ahora)` → `false`; tras `RegistrarFalla(T, …)` con una copia vencida: `false` a T+29 min y `true` a T+30 min; con `UltimaCopia` 40 días atrás, `Programar(mensual, día 1, …, ahora = día 10)` → `Toca(ahora)` es `false` (reprogramar reinicia la referencia). `Programar` inválido lanza `ArgumentException` (hora 24 y −1, minuto 60, conservar 0 y 61, semanal sin día y con día 8, mensual con día 0 y 29) y no cambia nada; en `Diaria` los días quedan `null`. `NombreDeArchivo(2026-11-08T01:05:09Z)` → `"gymstudio_auto_20261107_220509.zip"`. `EsNombreDeCopiaAutomatica`: verdadero para ese nombre; falso para `null`, `""`, `"../gymstudio_auto_20261107_220509.zip"`, `"a/gymstudio_auto_20261107_220509.zip"`, `"gymstudio_auto_20261107_220509.zip.tmp"`, `"gymstudio_auto_20261107_220509.zip\n"`, `"gymstudio_auto_x.zip"`, `"GYMSTUDIO_AUTO_20261107_220509.ZIP"`.
- [ ] **Step 2: Correr y ver el fallo.** `dotnet test backend/tests/EstudioContable.Domain.Tests --filter "FullyQualifiedName~ReglasDeCopiasAutomaticas"` → no compila.
- [ ] **Step 3: Implementar** los tres archivos según *Interfaces*. `ProximaEjecucion` trabaja en hora argentina: para las frecuencias de 6 y 12 horas los momentos son `Hora:Minuto + k·intervalo` de cada día; para las demás, el primer día calendario que cumple (día de la semana o del mes) desde la fecha de la referencia, sin pasar de un mes cuando el día ya pasó.
- [ ] **Step 4: Correr y ver que pasan.** Mismo comando → verde.
- [ ] **Step 5: Commit.** `git add backend/src/EstudioContable.Domain backend/tests/EstudioContable.Domain.Tests` · `Copias automáticas: configuración y regla de horario (dominio)`.

---

## Task 2: Tabla de configuración y migración

**Files:**
- Modify: `backend/src/EstudioContable.Application/Interfaces/IAppDbContext.cs`, `backend/src/EstudioContable.Infrastructure/AppDbContext.cs`, `backend/src/EstudioContable.Application/Copias/CopiasDeSeguridad.cs` (etiqueta)
- Create: migración `CopiasAutomaticas` en `backend/src/EstudioContable.Infrastructure/Migrations/`
- Test: `backend/tests/EstudioContable.IntegrationTests/ConfiguracionDeCopiasBaseTests.cs`

**Interfaces:**
- Consumes: `ConfiguracionDeCopias`, `FrecuenciaDeCopia` (Task 1).
- Produces: `DbSet<ConfiguracionDeCopias> ConfiguracionesDeCopias` en `IAppDbContext` y `AppDbContext`. Tabla `contabilidad."ConfiguracionDeCopias"` con la fila inicial `(1, false, 'Diaria', 22, 0, NULL, NULL, 14, now())`; `Frecuencia` guardada como texto (`HasConversion<string>()`, `varchar(20)`); `CHECK` : `CK_ConfiguracionDeCopias_Unica` (`"Id" = 1`), `_Hora` (0–23), `_Minuto` (0–59), `_Conservar` (1–60), `_DiaSemana` (nulo o 1–7), `_DiaMes` (nulo o 1–28), `_Frecuencia` (una de las cinco); trigger `trg_auditar_configuracion_copias` (`fn_auditar`); etiqueta `["contabilidad.ConfiguracionDeCopias"] = ("Programación de copias automáticas", "Sistema", 63)`.

- [ ] **Step 1: Escribir `ConfiguracionDeCopiasBaseTests` (fallan).** `Existe_la_fila_inicial_inactiva_y_diaria_a_las_22` (por SQL: una fila, `Id = 1`, `Activa = false`, `Frecuencia = 'Diaria'`, `Conservar = 14`); `La_base_rechaza_valores_imposibles_aunque_se_escriba_directo` (teoría con `UPDATE`: hora 24, minuto 60, conservar 0 y 61, día de la semana 8, día del mes 29, frecuencia `'Cada5Minutos'`, cada una con su nombre de restricción y `CheckViolation`); `No_admite_una_segunda_fila` (`INSERT` con `Id = 2` → `CK_ConfiguracionDeCopias_Unica`; con `Id = 1` → violación de la clave primaria); `La_copia_de_seguridad_incluye_la_programacion` (CSV `datos/contabilidad.ConfiguracionDeCopias.csv` y, en `comparar`, etiqueta, grupo `Sistema` y estado `igual`).
- [ ] **Step 2: Correr y ver el fallo.** `dotnet test backend/tests/EstudioContable.IntegrationTests --filter "FullyQualifiedName~ConfiguracionDeCopiasBaseTests"` → no compila.
- [ ] **Step 3: Mapear y migrar.** `DbSet` en ambas, mapeo en `AppDbContext.OnModelCreating` (`ValueGeneratedNever` para `Id`, `CHECK` con las constantes del dominio), etiqueta en `EtiquetasDeTablas`. `dotnet ef migrations add CopiasAutomaticas --project backend/src/EstudioContable.Infrastructure --startup-project backend/src/EstudioContable.API` y completar a mano la fila inicial (`migrationBuilder.InsertData` o `Sql`) y el trigger de auditoría.
- [ ] **Step 4: Correr y ver que pasan.** Mismo comando, más `dotnet ef migrations has-pending-model-changes …` sin cambios pendientes y la suite de `CopiasDeSeguridadTests` en verde (la restauración recorre las tablas nuevas sola).
- [ ] **Step 5: Commit.** `git add backend` · `Copias automáticas: tabla de configuración con restricciones y auditoría`.

---

## Task 3: Servicio que genera, guarda, rota y lista las copias

**Files:**
- Create: `backend/src/EstudioContable.Application/Copias/CopiasAutomaticas.cs` (interfaz y registros), `backend/src/EstudioContable.Infrastructure/Copias/CopiasAutomaticasService.cs`
- Modify: `backend/src/EstudioContable.API/Program.cs` (registro de servicios, ≈ línea 193), `backend/tests/EstudioContable.IntegrationTests/ApiFixture.cs`
- Create: `backend/tests/EstudioContable.IntegrationTests/RelojDePrueba.cs`
- Test: `backend/tests/EstudioContable.IntegrationTests/CopiasAutomaticasServicioTests.cs`

**Interfaces:**
- Consumes: `ReglasDeCopiasAutomaticas`, `ConfiguracionDeCopias` (Tasks 1–2); `ICopiasDeSeguridadService.GenerarAsync`/`CompararAsync`, `IRegistroDeEventos.RegistrarAsync(tabla, operacion, usuario, ct)`, `IUsuarioActual.Email`.
- Produces:
  ```csharp
  public record ProgramacionDeCopias(bool Activa, string? Frecuencia, int Hora, int Minuto, int? DiaDeLaSemana, int? DiaDelMes, int Conservar);
  public record EstadoDeCopiasAutomaticas(bool Activa, string Frecuencia, int Hora, int Minuto, int? DiaDeLaSemana, int? DiaDelMes, int Conservar,
      DateTimeOffset? ProximaCopia, DateTimeOffset? UltimaCopia, DateTimeOffset? UltimoIntento, string? UltimoError);
  public record CopiaGuardada(string Nombre, long Bytes, DateTimeOffset Fecha);
  public interface ICopiasAutomaticasService {
      Task<EstadoDeCopiasAutomaticas> ObtenerAsync(CancellationToken ct);      // crea la fila por defecto si falta
      Task<EstadoDeCopiasAutomaticas> ProgramarAsync(ProgramacionDeCopias pedido, CancellationToken ct);  // ArgumentException si es inválida; registra PROGRAMACION
      Task<CopiaGuardada> GuardarAhoraAsync(CancellationToken ct);             // espera el semáforo; registra GUARDADA
      Task<CopiaGuardada?> EjecutarSiTocaAsync(CancellationToken ct);          // null si no toca o hay otra corrida; nunca lanza por una falla de guardado
      IReadOnlyList<CopiaGuardada> Listar();                                   // más nueva primero
      Stream? AbrirParaDescargar(string nombre);                               // null si el nombre no es válido o no existe
  }
  // Infraestructura de pruebas
  public sealed class RelojDePrueba : TimeProvider { public void Fijar(DateTimeOffset ahora); public void Liberar(); /* GetUtcNow(): fijo o real */ }
  // ApiFixture: public RelojDePrueba Reloj; public string CarpetaDeCopias; public IServiceProvider Servicios;
  //   usa UseSetting("Copias:Carpeta", <carpeta temporal única>) y UseSetting("Copias:TareaAutomatica", "false"), registra Reloj como TimeProvider, borra la carpeta al terminar.
  ```
  Reglas del servicio: `Frecuencia` se acepta solo si es el nombre exacto de un valor definido (sin números como `"3"` ni `"99"`, sin vacío). Guardar = crear la carpeta, borrar los `*.zip.tmp` huérfanos, `GenerarAsync`, escribir `…zip.tmp` y renombrar a `NombreDeArchivo(ahora)` (sobrescribe si es el mismo segundo), `RegistrarCopia`, retención (las `Conservar` más nuevas entre los nombres válidos; un fallo al borrar se registra en el log y no falla la copia). Falla al guardar: `RegistrarFalla` con el mensaje fijo «No se pudo guardar la copia en la carpeta de copias automáticas. Revisá los permisos y el espacio en disco.» (el detalle va al log), evento `FALLIDA`, devuelve `null`. Un `SemaphoreSlim` estático de una plaza: `EjecutarSiTocaAsync` usa `WaitAsync(0)`, `GuardarAhoraAsync` espera. `AbrirParaDescargar` valida con `EsNombreDeCopiaAutomatica`, arma la ruta y comprueba que la ruta completa quede dentro de la carpeta.

- [ ] **Step 1: Escribir `CopiasAutomaticasServicioTests` y la infraestructura de pruebas (fallan).** Cada prueba deja la configuración inactiva y la carpeta vacía al empezar y `Reloj.Liberar()` al terminar. Pruebas (todas con el servicio resuelto desde `api.Servicios.CreateScope()`): `No_hace_nada_si_esta_inactiva`; `Hace_la_copia_cuando_toca_y_no_antes` (diaria 22:00 programada el 7/11 a las 15:00; con el reloj en 21:59 → `null` y carpeta vacía; en 22:00 → existe `gymstudio_auto_20261107_220000.zip`; `CompararAsync` sobre ese archivo da firma `"valida"`); `No_repite_dentro_del_mismo_periodo` (a las 22:00:30 → `null`, un solo archivo); `Si_estuvo_apagado_varios_periodos_hace_una_sola_copia` (última copia hace 3 días, ahora 23:00 → un archivo y la llamada siguiente `null`); `La_retencion_borra_solo_las_copias_propias` (`Conservar = 2` con tres copias viejas y además `notas.zip`, `gymstudio_auto_x.zip`, `copia.dump` y una subcarpeta: quedan las 2 más nuevas válidas y todo lo ajeno intacto); `Un_temporal_huerfano_se_borra`; `Una_falla_se_guarda_y_se_reintenta_a_los_30_minutos` (se reemplaza la carpeta por un **archivo** con el mismo nombre para que no se pueda crear: no lanza, `UltimoError` tiene el mensaje fijo, a +29 min no hay nuevo intento — `UltimoIntento` no cambia —, a +30 min sí lo hay; al devolver la carpeta se guarda y `UltimoError` queda en `null`); `Hacer_una_copia_ahora_guarda_cuenta_como_ultima_y_no_rompe_la_proxima`; `Una_corrida_automatica_y_una_copia_ahora_a_la_vez_dejan_dos_zip_validos` (`Task.WhenAll`; ambos se abren con `ZipArchive`); `Programar_rechaza_lo_imposible` (teoría: semanal sin día, semanal día 8, mensual día 0 y 29 a 31, hora 24, minuto 60, conservar 0 y 1000, frecuencia `null`, `""`, `"99"`, `"3"` y `"Cada5Minutos"` → `ArgumentException` y la configuración no cambia); `Listar_ordena_de_la_mas_nueva_a_la_mas_vieja_e_ignora_lo_ajeno`; `AbrirParaDescargar_solo_abre_copias_de_la_carpeta` (válido → `Stream`; `null` para `"../x"`, `"..\\x"`, una ruta absoluta, `"%2e%2e%2fappsettings.json"`, el nombre válido con `\n` final, `…zip.tmp`, un nombre válido inexistente); `Quedan_en_la_auditoria_la_programacion_y_cada_copia` (vía `/api/auditoria?tabla=copia`: `PROGRAMACION` con el email del administrador, `AUTOMATICA` y `FALLIDA` con «sistema», `GUARDADA`).
- [ ] **Step 2: Correr y ver el fallo.** `dotnet test backend/tests/EstudioContable.IntegrationTests --filter "FullyQualifiedName~CopiasAutomaticasServicioTests"` → no compila.
- [ ] **Step 3: Implementar** `CopiasAutomaticas.cs`, `CopiasAutomaticasService.cs` (dependencias: `AppDbContext`, `ICopiasDeSeguridadService`, `IRegistroDeEventos`, `IUsuarioActual`, `TimeProvider`, `IConfiguration`, `ILogger<>`) y, en `Program.cs`, `AddSingleton(TimeProvider.System)` (solo si no hay otro registrado) y `AddScoped<ICopiasAutomaticasService, CopiasAutomaticasService>()`. Ajustar `ApiFixture` según *Interfaces*.
- [ ] **Step 4: Correr y ver que pasan.** Mismo comando; después `dotnet test backend/EstudioContable.slnx` completo.
- [ ] **Step 5: Commit.** `git add backend` · `Copias automáticas: servicio que guarda, rota y lista las copias`.

---

## Task 4: Tarea en segundo plano, API y Docker

**Files:**
- Create: `backend/src/EstudioContable.API/Copias/CopiasAutomaticasHostedService.cs`, `backend/src/EstudioContable.API/Controllers/CopiasAutomaticasController.cs`
- Modify: `backend/src/EstudioContable.API/Program.cs` (registro condicional del servicio en segundo plano), `docker-compose.yml`
- Test: `backend/tests/EstudioContable.IntegrationTests/CopiasAutomaticasApiTests.cs`

**Interfaces:**
- Consumes: `ICopiasAutomaticasService` y los registros de la Task 3.
- Produces:
  ```csharp
  public sealed class CopiasAutomaticasHostedService : BackgroundService {
      // espera ~20 s al arrancar y después un PeriodicTimer de 1 minuto
      public static Task UnaVueltaAsync(IServiceScopeFactory scopes, ILogger log, CancellationToken ct);  // abre un scope, llama EjecutarSiTocaAsync; captura y registra cualquier excepción menos la cancelación
  }
  // CopiasAutomaticasController: [Route("api/copias")] [Authorize(Roles = Roles.Administrador)]
  // GET    api/copias/programacion              → EstadoDeCopiasAutomaticas
  // PUT    api/copias/programacion              (ProgramacionDeCopias) → EstadoDeCopiasAutomaticas
  // POST   api/copias/programacion/ahora        → CopiaGuardada
  // GET    api/copias/automaticas               → CopiaGuardada[]
  // GET    api/copias/automaticas/{nombre}      → archivo application/zip; 404 si no existe o el nombre no es válido
  ```
  `docker-compose.yml`, servicio `backend`: `volumes: - ./backups:/backups` y `environment: - Copias__Carpeta=/backups/automaticas`.

- [ ] **Step 1: Escribir `CopiasAutomaticasApiTests` (fallan por 404).** `Solo_el_administrador_usa_estas_rutas` (las cinco rutas: 401 anónimo, 403 Contador y Recepción, sin efectos); `La_programacion_inicial_esta_inactiva_y_sin_proxima_copia`; `Programar_devuelve_la_proxima_copia` (reloj fijo el 7/11/2026 a las 15:00 hora argentina, diaria 22:00 → `proximaCopia` = 7/11/2026 22:00 −03:00); `Una_programacion_invalida_da_400_y_no_cambia_nada` (teoría con los casos imposibles del Review Focus, incluido el cuerpo roto y la frecuencia numérica); `Hacer_una_copia_ahora_aparece_en_la_lista_y_se_descarga_firmada` (la descarga se sube a `/api/copias/comparar`: firma `"valida"`); `Descargar_con_nombres_hostiles_no_sale_de_la_carpeta` (`..%2f..%2fappsettings.json`, `%2e%2e`, barra invertida, un nombre válido inexistente → 404, nunca contenido de otro archivo); `UnaVuelta_no_deja_caer_el_servicio_si_algo_falla` (un `IServiceScopeFactory` que lanza: `UnaVueltaAsync` termina sin excepción y registra el error).
- [ ] **Step 2: Correr y ver el fallo.** `dotnet test … --filter "FullyQualifiedName~CopiasAutomaticasApiTests"` → fallan por 404 / compilación.
- [ ] **Step 3: Implementar** el servicio en segundo plano (registrado solo si `Copias:TareaAutomatica` es `true`, que es el valor por defecto) y el controlador; la descarga devuelve `File(stream, "application/zip", nombre)`. Editar `docker-compose.yml`.
- [ ] **Step 4: Correr y ver que pasan.** Mismo comando, después la suite completa de `EstudioContable.slnx`.
- [ ] **Step 5: Commit.** `git add backend docker-compose.yml` · `Copias automáticas: tarea en segundo plano, API y carpeta compartida con la PC`.

---

## Task 5: Pantalla (frontend)

**Files:**
- Create: `frontend/src/features/configuracion/copiasAutomaticasUtil.ts`, `frontend/src/features/configuracion/CopiasAutomaticas.tsx`
- Modify: `frontend/src/api/copiasApi.ts`, `frontend/src/features/configuracion/ModuloConfiguracion.tsx` (dentro de `tabActiva === 'backup'`, ≈ línea 582: nueva sección después del bloque «Copia completa del sistema», y el texto de ≈ línea 601 que hoy solo habla de PowerShell), `frontend/src/features/manual/secciones.tsx` (sección `respaldo`)
- Test: `frontend/tests/copiasAutomaticas.test.ts`

**Interfaces:**
- Produces (`copiasAutomaticasUtil.ts`, sin dependencias para probarlo con Node):
  ```ts
  export type Frecuencia = 'CadaSeisHoras' | 'CadaDoceHoras' | 'Diaria' | 'Semanal' | 'Mensual'
  export interface Programacion { activa: boolean; frecuencia: Frecuencia; hora: number; minuto: number; diaDeLaSemana: number | null; diaDelMes: number | null; conservar: number }
  export interface EstadoDeCopias extends Programacion { proximaCopia: string | null; ultimaCopia: string | null; ultimoIntento: string | null; ultimoError: string | null }
  export interface CopiaGuardada { nombre: string; bytes: number; fecha: string }
  export const FRECUENCIAS: readonly { valor: Frecuencia; texto: string }[]   // «Cada 6 horas», «Cada 12 horas», «Todos los días», «Una vez por semana», «Una vez por mes»
  export function validarProgramacion(p: Programacion): Partial<Record<'hora' | 'minuto' | 'diaDeLaSemana' | 'diaDelMes' | 'conservar', string>>
  export function resumenDeProgramacion(p: Programacion): string
  export function textoDeFecha(iso: string | null): string      // hora de Argentina, es-AR
  export function tamanoLegible(bytes: number): string          // «532 B», «12,3 KB», «4,1 MB»
  ```
  `copiasApi.ts`: `obtenerProgramacion(): Promise<EstadoDeCopias>`, `guardarProgramacion(p: Programacion): Promise<EstadoDeCopias>`, `hacerCopiaAhora(): Promise<CopiaGuardada>`, `listarCopiasAutomaticas(): Promise<CopiaGuardada[]>`, `descargarCopiaAutomatica(nombre: string): Promise<Blob>` (mismo `pedir`).

- [ ] **Step 1: Escribir `copiasAutomaticas.test.ts` (fallan).** `validarProgramacion` acepta una programación válida y marca hora 24, minuto 60, conservar 0 y 61, semanal sin día, mensual con 0 y 29 (los mensajes nombran el rango, p. ej. «El día del mes va de 1 a 28.»); ignora el día que no corresponde a la frecuencia. `resumenDeProgramacion`: «Todos los días a las 22:00», «Cada 6 horas desde las 02:00 (02:00, 08:00, 14:00 y 20:00)», «Cada 12 horas desde las 06:30 (06:30 y 18:30)», «Los lunes a las 08:30», «El día 5 de cada mes a las 22:00». `textoDeFecha('2026-11-08T01:00:00Z')` contiene `22:00` y `7/11/2026`; `null` → `—`. `tamanoLegible(532)`, `(12595)`, `(4_300_000)`.
- [ ] **Step 2: Correr y ver el fallo.** PowerShell: `cd frontend; npm test` → falla por import inexistente.
- [ ] **Step 3: Implementar `copiasAutomaticasUtil.ts` y los clientes de `copiasApi.ts`**; `npm test` verde.
- [ ] **Step 4: Componente `CopiasAutomaticas`.** Interruptor «Copia automática activada»; select de frecuencia; hora (`<input type="time">`); select de día de la semana solo en semanal; número de día solo en mensual; número «Copias a conservar»; «Guardar programación» (desactivado sin cambios o con errores); «Hacer una copia ahora» con indicador de progreso; resumen en lenguaje natural, **Próxima copia** y **Última copia** (con «—» si no hay); el último error en un `role="alert"` rojo («La última copia automática falló: …»); lista de copias guardadas (nombre, fecha, tamaño, **Descargar**) o «Todavía no hay copias automáticas»; una línea que dice dónde se guardan («carpeta `backups\automaticas` del proyecto») y que Docker tiene que estar abierto a esa hora. Errores de validación junto a cada campo (`aria-invalid`, `aria-describedby`, foco en el primero con error); el mensaje del servidor que nombre un campo va junto a él; tostada solo para éxito. Insertarlo en `ModuloConfiguracion.tsx` y actualizar el texto que solo habla de PowerShell; sección `respaldo` del manual con «Copia automática» (cómo programarla, dónde queda, qué pasa si estaba apagado, que restaurar devuelve también la programación).
- [ ] **Step 5: Verificar.** `npm test`, `npm run lint` y `npm run build` sin errores. Revisión visual en el navegador integrado (Docker aparte de la Task 6) a 1280 px y 375 px: bloque nuevo, errores junto a cada campo, estado de error, lista vacía y con copias.
- [ ] **Step 6: Commit.** `git add frontend` · `Copias automáticas: bloque de programación en Configuración y manual`.

---

## Task 6: Documentación, verificación de punta a punta y entrega

**Files:**
- Modify: `docs/BACKUPS.md` (sección «Hacerla automática» con los dos caminos: desde el sistema y con el Programador de tareas; «Cómo se probó»), `docs/SEGURIDAD.md`, `docs/POLITICA_DE_SEGURIDAD.md` (si habla de copias), `docs/ARQUITECTURA.md` (tarea en segundo plano), `docs/BASE_DE_DATOS.md` (tabla nueva), `README.md`

- [ ] **Step 1: Documentar.** Incluir: formato y firma iguales a los de hoy; carpeta y volumen de Docker (hay que recrear el contenedor una vez); que Docker tiene que estar prendido y que, si estuvo apagado, se hace una copia al volver; retención; que restaurar devuelve la programación; que la copia de una versión anterior se compara pero no se restaura; que no están cifradas.
- [ ] **Step 2: Docker aparte** (`-p copiasauto`, puertos 5222 / 5273 / 5533, volumen `./backups` apuntando a una carpeta del scratchpad). Recorrido real como Administrador: activar una programación diaria a la hora argentina de dentro de 2 minutos y esperar a que aparezca sola el archivo en la carpeta del host y en la lista; «Hacer una copia ahora»; descargar y subir esa copia en «Seleccionar copia» (firma válida, «idéntica»); poner la carpeta en solo lectura y ver el error en pantalla; Contador y Recepción no ven el bloque; revisar la Auditoría.
- [ ] **Step 3: Suite completa.** `dotnet test backend/EstudioContable.slnx` y, desde PowerShell, `cd frontend; npm test; npm run lint; npm run build`: todo verde.
- [ ] **Step 4: Base del usuario.** `scripts\backup.ps1`, luego `docker compose up -d` (recrea el backend con el volumen nuevo) y comprobar en `http://localhost:5173` que la pantalla carga y que «Hacer una copia ahora» deja un archivo en `backups\automaticas`. Bajar el Docker aparte y borrar sus volúmenes y carpetas.
- [ ] **Step 5: Commit.** `git add docs README.md` · `Documentación de las copias automáticas`.
- [ ] **Step 6: Zip y memoria.** Antes de sobrescribir, ver la estructura del `GymStudio-ERP.zip` actual (`unzip -l … | head`) y regenerarlo con `git archive --format=zip` respetando ese mismo prefijo (el zip está ignorado por git). Actualizar `memory/video-demo.md` con que los capítulos de Calendario y de Copia de seguridad del video quedaron desactualizados, y ofrecerle al usuario volver a grabarlos.
