# Entrenadores: horas cumplidas y presupuesto de pago · Plan de implementación (Parte B de 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que el Administrador y el Contador carguen el valor de la hora de cada entrenador y las horas que cumplió por día (precargadas desde el calendario), y obtengan un presupuesto de pago por período, en pantalla y en PDF.

**Architecture:** Una columna `ValorHora` en la ficha y una tabla `HorasDeEntrenador` (un registro por entrenador y día, con el valor de la hora congelado al crearlo). Un servicio de aplicación hace las cuatro operaciones (ver, guardar, precargar, presupuesto) con reglas puras en el dominio; el reloj entra como `TimeProvider`. En la pantalla, `ModuloEntrenadores` gana las pestañas **Horas** y **Presupuesto** solo para la gestión.

**Tech Stack:** .NET 10 / EF Core / PostgreSQL 15 (xUnit + Testcontainers), React 19 + TypeScript + Tailwind + jsPDF (pruebas con `node --test`).

**Spec:** `docs/superpowers/specs/2026-11-08-entrenadores-menu-horas-y-presupuesto-design.md` (sección «Parte B»). **Se hace después de fusionar la Parte A** (`2026-11-08-entrenadores-menu-foto-y-contacto.md`): usa `ModuloEntrenadores`, `PestanaEntrenadores`, `ModalEntrenador` y los campos `email` y `whatsApp` que esa parte crea. Se ejecuta en un worktree nuevo cortado de `main` ya fusionada.

## Global Constraints

- Se trabaja **solo** en un worktree (`C:\Sistema_contable_wt`); no se edita `C:\Sistema_contable`, cuyo Docker monta `./backend` con `dotnet watch` y migraría la base real. No se toca ese Docker.
- Textos en castellano rioplatense (voseo), sin emojis. Errores del servidor: `ArgumentException` / `InvalidOperationException` (400), `NotFoundException` (404). **Todas las rutas de horas y presupuesto son `Roles.Gestion`**: 401 sin sesión, 403 a Recepción.
- `ValorHora`: `decimal(12,2)` nulo (nulo = sin valor), de 0 a 999 999 999 999, **hasta 2 decimales** (más se rechaza, no se redondea en silencio). Solo lo ven y lo cargan el Administrador y el Contador: para Recepción la API devuelve `valorHora: null` (lo oculta el servidor). Editar la ficha con `valorHora` ausente o nulo lo borra.
- `HorasDeEntrenador`: `Horas` de 0 a 24 y múltiplo de **0,25** (0 = faltó); `ValorHora` no nulo, copiado del entrenador al **crear** el día y que no cambia si el del entrenador sube; `Nota` hasta **200** caracteres sin caracteres de control; un registro por entrenador y día (`ux_horas_entrenador_fecha`); `Fecha` no posterior a hoy en **hora de Argentina (UTC-3)**; la clave foránea al entrenador es `RESTRICT`.
- Período: `desde <= hasta`, de hasta **62 días** (contando los dos extremos). `PUT` de horas: hasta **500** renglones, todo o nada. Monto de un día = `Horas × ValorHora` redondeado a 2 decimales (`MidpointRounding.AwayFromZero`); monto de un entrenador = suma de los montos de sus días; total = suma de los entrenadores. Todo con `decimal`.
- Frontend: sin errores de lint nuevos (pedir dentro del efecto con `.then` y bandera de cancelación); no abrir un `<form>` dentro de otro; importes en formato argentino (`$ 1.234,56`); el PDF en blanco y negro con `pdfComun`. Pruebas con `node --experimental-strip-types --test tests/*.test.ts` desde `frontend/`.
- Trampas del entorno: en heredocs de Python por la herramienta Bash, `\\` llega como `\` y `\00` se vuelve un NUL (usar `chr(92)` o Edit/Write); probar la API con Python, no con `curl`; `auditoria.operacion` es `varchar(10)`.
- Cada tarea termina con la suite completa en verde (`dotnet test backend/EstudioContable.slnx` y las del frontend). Commits con el trailer `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.

## Review Focus

- Subir el valor de la hora después de cargar días: los días ya cargados conservan el valor que tenían (Task 3).
- Un entrenador sin valor de hora: no se le puede crear un día ni se le precarga nada, y el mensaje dice a quién le falta (Tasks 3 y 5).
- Recepción pidiendo por la API horas, presupuesto o el valor de la hora: 403, y `valorHora: null` en la lista (Tasks 2 y 3).
- Medio centavo: `0,25 h × $ 100,10` son $ 25,03 por día, y dos días suman $ 50,06 (se redondea cada día, no la suma) (Tasks 1 y 3).
- Cerca de la medianoche: a las 22:30 de Argentina (01:30 UTC del día siguiente) «hoy» es el día argentino, y el día siguiente es futuro (Task 3). Período invertido, de más de 62 días o con más de 500 renglones: 400 sin cambiar nada (Task 3).

---

## Task 1: Dominio: valor de la hora, horas del día y reglas

**Files:**
- Modify: `backend/src/EstudioContable.Domain/Entities/Entrenador.cs`
- Create: `backend/src/EstudioContable.Domain/Entities/HoraDeEntrenador.cs`, `backend/src/EstudioContable.Domain/ReglasDeHoras.cs`
- Test: `backend/tests/EstudioContable.Domain.Tests/ReglasDeHorasTests.cs`, `backend/tests/EstudioContable.Domain.Tests/EntrenadorTests.cs` (agregar)

**Interfaces:**
- Produces:
  ```csharp
  // Entrenador: parámetro opcional al final de ambos
  public decimal? ValorHora { get; private set; }
  public Entrenador(string? nombreCompleto, string? telefono, string? especialidad, string? email = null, string? whatsApp = null, decimal? valorHora = null);
  public void ActualizarDatos(string? nombreCompleto, string? telefono, string? especialidad, string? email = null, string? whatsApp = null, decimal? valorHora = null);

  public static class ReglasDeHoras {
      public const int DiasMaximosDelPeriodo = 62, LargoMaximoDeLaNota = 200, RenglonesMaximos = 500;
      public static DateOnly HoyEnArgentina(DateTimeOffset ahora);                 // ahora.ToOffset(-3h) → DateOnly
      public static int DiaDeLaSemana(DateOnly fecha);                              // lunes = 1 … domingo = 7
      public static int Programadas(IEnumerable<(long EntrenadorId, int DiaSemana)> turnos, long entrenadorId, DateOnly fecha);
      public static decimal ValidarHoras(decimal horas);                            // 0–24, múltiplo de 0,25
      public static decimal? ValidarValorHora(decimal? valor);                      // null vale; 0–999 999 999 999; hasta 2 decimales
      public static void ValidarFecha(DateOnly fecha, DateOnly hoy);                // no posterior a hoy
      public static void ValidarPeriodo(DateOnly desde, DateOnly hasta);            // desde <= hasta y hasta 62 días
      public static string? LimpiarNota(string? nota);
      public static decimal MontoDelDia(decimal horas, decimal valorHora);
  }
  public class HoraDeEntrenador {
      public long Id { get; private set; }  public long EntrenadorId { get; private set; }  public DateOnly Fecha { get; private set; }
      public decimal Horas { get; private set; }  public decimal ValorHora { get; private set; }  public string? Nota { get; private set; }
      protected HoraDeEntrenador() { }
      public HoraDeEntrenador(long entrenadorId, DateOnly fecha, decimal horas, decimal valorHora, string? nota, DateOnly hoy);
      public void Cambiar(decimal horas, string? nota);                              // el valor y la fecha no cambian
  }
  ```
  Todos lanzan `ArgumentException` con un mensaje claro en castellano; ninguno cambia el estado si falla.

- [ ] **Step 1: Escribir las pruebas (fallan por compilación).** `ReglasDeHorasTests`: `MontoDelDia` (`8 × 1500,00` = `12000,00`; `7,5 × 1234,56` = `9259,20`; `0,25 × 100,10` = `25,03`; `0,25 × 0,10` = `0,03`; `0,25 × 0,30` = `0,08`: el medio centavo sube); `DiaDeLaSemana` (2026-11-09 lunes = 1, 2026-11-08 domingo = 7, 2026-11-14 sábado = 6); `Programadas` cuenta solo los turnos de ese entrenador en ese día de la semana (y da 0 en sábado y domingo); `HoyEnArgentina` (`2026-11-13T01:30Z` → 2026-11-12; `2026-11-12T03:00Z` → 2026-11-12; `2026-11-12T02:59Z` → 2026-11-11); `ValidarHoras` (0, 0,25, 7,5 y 24 valen; −0,25, 24,25, 0,1 y 0,3 lanzan); `ValidarValorHora` (null, 0 y `1234,56` valen; −1, `1000000000000`, `1,234` lanzan); `ValidarFecha` (hoy y ayer valen; mañana lanza); `ValidarPeriodo` (un solo día vale; 2026-10-01 → 2026-12-01 son 62 días y valen; hasta 2026-12-02 son 63 y lanzan; invertido lanza); `LimpiarNota` (recorta, `""` → null, 201 caracteres y `"a\nb"` lanzan). `HoraDeEntrenadorTests` (mismo archivo): se crea con sus valores, `Cambiar` solo cambia horas y nota, un cambio inválido no cambia nada, una fecha futura lanza. `EntrenadorTests`: `ValorHora` nulo, 0 y 1500,50 valen; negativo, enorme y de 3 decimales lanzan; `ActualizarDatos` con un valor inválido no cambia nada; sin `valorHora` lo borra.
- [ ] **Step 2: Correr y ver el fallo.** `dotnet test backend/tests/EstudioContable.Domain.Tests --filter "FullyQualifiedName~ReglasDeHoras|FullyQualifiedName~HoraDeEntrenador|FullyQualifiedName~EntrenadorTests"` → no compila.
- [ ] **Step 3: Implementar** según *Interfaces*. `ValidarHoras` comprueba `horas * 4 == decimal.Truncate(horas * 4)`; `ValidarValorHora` usa `ReglasDeDatos.Importe` y `valor == Math.Round(valor, 2)`; `ValidarPeriodo` cuenta `(hasta.DayNumber - desde.DayNumber) + 1`.
- [ ] **Step 4: Correr y ver que pasan.** El comando del Step 2 y toda la suite de dominio.
- [ ] **Step 5: Commit.** `git add backend` · `Horas de entrenadores: valor de la hora, día cargado y reglas (dominio)`.

---

## Task 2: Datos: migración, restricciones y valor de la hora en la API

**Files:**
- Modify: `backend/src/EstudioContable.Infrastructure/AppDbContext.cs`, `backend/src/EstudioContable.Application/Interfaces/IAppDbContext.cs`, `backend/src/EstudioContable.Application/Copias/CopiasDeSeguridad.cs`, `backend/src/EstudioContable.API/Middlewares/ExceptionMiddleware.cs` (mensaje de `TraducirErrorDeBase`), `backend/src/EstudioContable.API/Controllers/EntrenadoresController.cs`
- Create: migración `HorasYValorDeEntrenador` en `backend/src/EstudioContable.Infrastructure/Migrations/`
- Test: `backend/tests/EstudioContable.IntegrationTests/HorasDeEntrenadorBaseTests.cs`, `backend/tests/EstudioContable.IntegrationTests/MigracionDeHorasTests.cs`, `backend/tests/EstudioContable.IntegrationTests/EntrenadoresTests.cs` (agregar)

**Interfaces:**
- Consumes: Task 1.
- Produces: `DbSet<HoraDeEntrenador> HorasDeEntrenador { get; }` (`IAppDbContext` y `AppDbContext`); `EntrenadorDto` suma `decimal? ValorHora` (al final) y `GuardarEntrenadorRequest` suma `decimal? ValorHora`. Base: `Entrenadores."ValorHora"` `numeric(12,2)` nula con `CK_Entrenadores_ValorHora` (`"ValorHora" IS NULL OR "ValorHora" BETWEEN 0 AND 999999999999`); tabla `contabilidad."HorasDeEntrenador"` (`Id` identidad, `EntrenadorId`, `Fecha` `date`, `Horas` `numeric(4,2)`, `ValorHora` `numeric(12,2)`, `Nota` `varchar(200)` nula) con `CK_HorasDeEntrenador_Horas` (`"Horas" BETWEEN 0 AND 24 AND "Horas" * 4 = trunc("Horas" * 4)`), `CK_HorasDeEntrenador_ValorHora` (`BETWEEN 0 AND 999999999999`), clave foránea `FK_HorasDeEntrenador_Entrenadores_EntrenadorId` `RESTRICT` con índice, índice único `ux_horas_entrenador_fecha` (`EntrenadorId`, `Fecha`) y disparador `trg_auditar_horas_entrenador` (`fn_auditar`, creado en la migración). Etiqueta de la copia: `["contabilidad.HorasDeEntrenador"] = ("Horas de los entrenadores", "Gimnasio", 61)`. Mensaje 400 para `23505` con `ux_horas_entrenador_fecha`: «Ese entrenador ya tiene cargado ese día.».

- [ ] **Step 1: Escribir las pruebas (fallan).** `HorasDeEntrenadorBaseTests` (SQL directo contra la base compartida): `La_base_rechaza_horas_imposibles` (`Horas` 24,25, 0,1 y −1 → `CK_HorasDeEntrenador_Horas`; `ValorHora` −1 → `CK_HorasDeEntrenador_ValorHora`; `Entrenadores.ValorHora` −1 → `CK_Entrenadores_ValorHora`); `No_admite_dos_registros_del_mismo_entrenador_y_dia` (`UniqueViolation`, `ux_horas_entrenador_fecha`); `No_deja_borrar_a_un_entrenador_con_horas` (`ForeignKeyViolation`); `Las_horas_quedan_en_la_auditoria` (`INSERT` y `UPDATE` en `contabilidad.HorasDeEntrenador`); `Las_horas_entran_en_la_copia_de_seguridad` (entrada `datos/contabilidad.HorasDeEntrenador.csv` y `comparar` la da `igual` con la etiqueta). `MigracionDeHorasTests`: con `BasesDePrueba`, migrar hasta la migración de la Parte A, crear un entrenador, migrar a la última (`ValorHora` nulo, tabla vacía), volver atrás (sin columna ni tabla, el entrenador sigue) y subir de nuevo. `EntrenadoresTests`: `La_gestion_ve_y_carga_el_valor_de_la_hora_y_Recepcion_siempre_ve_null` (Administrador y Contador reciben `valorHora`; Recepción recibe `null` en la lista aunque tenga valor); `Editar_sin_valor_de_hora_lo_borra`; `Un_valor_de_hora_invalido_da_400_y_no_cambia_nada` (−1, `1000000000000`, `1,234`); Recepción no puede cambiarlo (403).
- [ ] **Step 2: Correr y ver el fallo.** `dotnet test backend/tests/EstudioContable.IntegrationTests --filter "FullyQualifiedName~HorasDeEntrenadorBase|FullyQualifiedName~MigracionDeHoras|FullyQualifiedName~EntrenadoresTests"` → fallan (columna y tabla inexistentes).
- [ ] **Step 3: Persistencia y migración.** Mapear `ValorHora` y `HoraDeEntrenador` (largos, `CK` con `HasCheckConstraint`, índice único, `HasOne<Entrenador>().WithMany().HasForeignKey(h => h.EntrenadorId).OnDelete(Restrict)`) y el `DbSet`. `dotnet ef migrations add HorasYValorDeEntrenador --project backend/src/EstudioContable.Infrastructure --startup-project backend/src/EstudioContable.API`, completar a mano el disparador de auditoría y revisar que el `Down` sea simétrico y que la migración no pierda datos. Etiqueta de la copia y mensaje del `23505`.
- [ ] **Step 4: Controlador.** `Listar` devuelve `ValorHora` solo si `User.IsInRole(Roles.Administrador) || User.IsInRole(Roles.Contador)`; si no, `null`. Alta y edición pasan `ValorHora` a la entidad (ausente = borra).
- [ ] **Step 5: Correr y ver que pasan.** El comando del Step 2, `dotnet ef migrations has-pending-model-changes ...` sin cambios y la suite completa.
- [ ] **Step 6: Commit.** `git add backend` · `Horas de entrenadores: migración, restricciones y valor de la hora (solo para la gestión)`.

---

## Task 3: Servicio y API de horas y presupuesto

**Files:**
- Create: `backend/src/EstudioContable.Application/Entrenadores/HorasDeEntrenadores.cs` (interfaz y registros), `backend/src/EstudioContable.Infrastructure/Entrenadores/HorasDeEntrenadoresService.cs`, `backend/src/EstudioContable.API/Controllers/HorasDeEntrenadoresController.cs`
- Modify: `backend/src/EstudioContable.API/Program.cs` (registro del servicio)
- Test: `backend/tests/EstudioContable.IntegrationTests/HorasDeEntrenadoresTests.cs`

**Interfaces:**
- Consumes: Tasks 1 y 2; `TimeProvider` (en las pruebas, `api.Reloj.Fijar(...)`); `IRegistroDeEventos` no hace falta: el disparador audita cada fila.
- Produces:
  ```csharp
  public record EntrenadorDeHoras(long Id, string Nombre, decimal? ValorHora);
  public record DiaDeHoras(long EntrenadorId, DateOnly Fecha, int Programadas, decimal? Cumplidas, decimal? ValorHora, string? Nota);
  public record HorasDelPeriodo(IReadOnlyList<EntrenadorDeHoras> Entrenadores, IReadOnlyList<DiaDeHoras> Dias);
  public record CambioDeHoras(long EntrenadorId, DateOnly Fecha, decimal? Horas, string? Nota);
  public record ResultadoDePrecarga(int Creados, IReadOnlyList<string> SinValor);
  public record FilaDePresupuesto(long EntrenadorId, string Nombre, int Dias, decimal Horas, decimal? ValorHora, decimal Monto);
  public record Presupuesto(DateOnly Desde, DateOnly Hasta, IReadOnlyList<FilaDePresupuesto> Filas, decimal TotalHoras, decimal TotalMonto);
  public interface IHorasDeEntrenadoresService {
      Task<HorasDelPeriodo> ObtenerAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
      Task GuardarAsync(IReadOnlyList<CambioDeHoras> cambios, CancellationToken ct);
      Task<ResultadoDePrecarga> PrecargarAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
      Task<Presupuesto> PresupuestoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
  }
  // GET  api/entrenadores/horas?desde=aaaa-mm-dd&hasta=aaaa-mm-dd            → HorasDelPeriodo
  // PUT  api/entrenadores/horas        { "items": [{ entrenadorId, fecha, horas, nota }] } → 204
  // POST api/entrenadores/horas/precargar { desde, hasta }                  → ResultadoDePrecarga
  // GET  api/entrenadores/presupuesto?desde=...&hasta=...                    → Presupuesto
  ```
  Reglas del servicio: `ObtenerAsync` devuelve los entrenadores activos más los desactivados con registros en el período, y un renglón por cada día con `Programadas > 0` **o** con registro (`Cumplidas` nulo si no hay registro); `GuardarAsync` valida todo antes de tocar la base y falla entero: entrenador inexistente, fecha futura (hoy = `HoyEnArgentina(reloj)`), horas inválidas, nota inválida, un mismo entrenador y día repetido, más de 500 renglones, **un día nuevo para un entrenador sin valor de hora** («Cargá el valor de la hora de X antes de cargarle horas.») o desactivado; los registros existentes conservan su `ValorHora`; `Horas = null` borra el registro. `PrecargarAsync` crea, para cada entrenador **activo con valor** y cada día del período hasta hoy con `Programadas > 0` y sin registro, un día con `Horas = Programadas` y el valor vigente; informa en `SinValor` los nombres de los activos sin valor que tendrían al menos un día para crear. `PresupuestoAsync` solo trae entrenadores con registros en el período; `Dias` cuenta los días con `Horas > 0`; `ValorHora` es el valor si fue el mismo en todo el período y `null` si hubo más de uno; `Monto` = suma de `MontoDelDia`.

- [ ] **Step 1: Escribir las pruebas (fallan: 404 en las rutas).** Todas dejan la tabla de horas y el calendario vacíos al terminar y usan entrenadores de nombre único. `Solo_la_gestion_usa_las_rutas_de_horas_y_presupuesto` (las cuatro: 401 anónimo, 403 Recepción, 200 o 204 Administrador y Contador). `El_periodo_trae_las_programadas_del_calendario_y_lo_cargado` (calendario real: Ana lunes 06–09 = 3 horas y miércoles 06–07 = 1; reloj fijo el jueves 2026-11-12; período 2026-11-02 al 2026-11-15; `Programadas` 3 y 1 en los lunes y miércoles; un día de fin de semana cargado a mano aparece con `Programadas` 0). `Precargar_crea_los_dias_hasta_hoy_sin_pisar_y_es_idempotente` (con el período hasta 2026-11-30 crea solo los de hasta el 12/11; un día ya cargado con 2 h conserva sus 2 h; repetir da `creados: 0`). `Precargar_informa_a_los_entrenadores_sin_valor_y_no_les_carga_nada`. `El_valor_guardado_de_un_dia_no_cambia_si_sube_el_valor_del_entrenador` (valor 1000, precargar, subir a 1500, los días siguen con 1000; un día **nuevo** creado después sale con 1500). `Guardar_crea_cambia_y_borra_dias` y `Un_pedido_invalido_se_rechaza_con_400_y_no_cambia_nada` (teoría: fecha futura, 24,25 h, 0,1 h, nota con salto de línea, nota de 201 caracteres, renglón repetido, entrenador inexistente, entrenador sin valor, entrenador desactivado con día nuevo, 501 renglones, cuerpo roto). `Cerca_de_la_medianoche_hoy_es_el_dia_argentino` (reloj en `2026-11-13T01:30:00Z`: cargar el 2026-11-12 vale y el 2026-11-13 da 400). `El_presupuesto_suma_los_dias_con_dos_valores_distintos` (2 días a 1000 y 2 a 1200, total de horas 8, monto 8800, `valorHora` nulo, `dias` 4; un día con 0 horas no cuenta en `dias` ni suma). `El_medio_centavo_se_redondea_por_dia` (valor 100,10; dos días de 0,25 h: monto 50,06, no 50,05). `Un_periodo_invertido_o_de_mas_de_62_dias_da_400` (en las rutas con período). `Un_entrenador_desactivado_conserva_sus_horas`. `Cargar_horas_queda_en_la_auditoria`.
- [ ] **Step 2: Correr y ver el fallo.** `dotnet test backend/tests/EstudioContable.IntegrationTests --filter "FullyQualifiedName~HorasDeEntrenadoresTests"` → fallan (404 y compilación).
- [ ] **Step 3: Implementar** el servicio (`AppDbContext`, `TimeProvider`) y el controlador (`[Route("api/entrenadores")]`, `[Authorize(Roles = Roles.Gestion)]`; `DateOnly` en la consulta y en el cuerpo; `GuardarHorasRequest(List<RenglonDeHorasDto?>? Items)` con renglones nulos o lista nula → `ArgumentException`). Registrar `IHorasDeEntrenadoresService` en `Program.cs`. Las horas programadas salen de `TurnosEncargado` del entrenador por día de la semana con `ReglasDeHoras.Programadas`.
- [ ] **Step 4: Correr y ver que pasan.** El comando del Step 2 y la suite completa.
- [ ] **Step 5: Commit.** `git add backend` · `Horas de entrenadores: ver, guardar, precargar y presupuesto (servicio y API)`.

---

## Task 4: Frontend: lógica de las horas y clientes de la API

**Files:**
- Create: `frontend/src/features/entrenadores/horasUtil.ts`, `frontend/src/api/horasApi.ts`
- Modify: `frontend/src/features/calendario/calendarioUtil.ts` (interfaz `Entrenador` suma `valorHora: number | null`), `frontend/src/api/entrenadoresApi.ts` (`DatosDeEntrenador` suma `valorHora: string`)
- Test: `frontend/tests/horas.test.ts`

**Interfaces:**
- Produces (todo sin dependencias en `horasUtil.ts`):
  ```ts
  export interface DiaDeHoras { entrenadorId: number; fecha: string; programadas: number; cumplidas: number | null; valorHora: number | null; nota: string | null }
  export interface HorasDelPeriodo { entrenadores: { id: number; nombre: string; valorHora: number | null }[]; dias: DiaDeHoras[] }
  export interface CambioDeHoras { entrenadorId: number; fecha: string; horas: number | null; nota: string | null }
  export type Borrador = Record<string, { horas: string; nota: string }>     // clave = claveDeDia(...); horas como se escribió ('' = sin cargar)
  export interface FilaDePresupuesto { entrenadorId: number; nombre: string; dias: number; horas: number; valorHora: number | null; monto: number }
  export interface Presupuesto { desde: string; hasta: string; filas: FilaDePresupuesto[]; totalHoras: number; totalMonto: number }
  export const claveDeDia = (entrenadorId: number, fecha: string) => string
  export function borradorDesde(dias: DiaDeHoras[]): Borrador
  export function parsearHoras(texto: string): number | null | 'invalida'     // '' → null; «7,5» y «7.5» → 7.5; fuera de 0–24 o no múltiplo de 0,25 → 'invalida'
  export function mensajeDeHoras(texto: string): string | null                // el error para mostrar junto al campo, o null
  export function cambiosDelBorrador(dias: DiaDeHoras[], borrador: Borrador): CambioDeHoras[]   // solo lo modificado; horas borradas → horas: null; nota vacía → null
  export function conDiaNuevo(dias: DiaDeHoras[], borrador: Borrador, entrenadorId: number, fecha: string): { dias: DiaDeHoras[]; borrador: Borrador }   // agrega un renglón con programadas 0 si no existe
  export function resumenPorEntrenador(dias: DiaDeHoras[], borrador: Borrador): { entrenadorId: number; cargadas: number; programadas: number }[]
  export function rangoDelMes(mes: string): { desde: string; hasta: string }  // 'aaaa-mm' → primer y último día
  export function nombreDelDia(fecha: string): string                         // 'lun' … 'dom'
  export function formatoDeHoras(horas: number): string                       // 7.5 → '7,5'; 8 → '8'
  export function formatoDeImporte(n: number): string                         // '$ 1.234,56'
  // horasApi.ts
  obtenerHoras(desde: string, hasta: string): Promise<HorasDelPeriodo>
  guardarHoras(cambios: CambioDeHoras[]): Promise<void>
  precargarHoras(desde: string, hasta: string): Promise<{ creados: number; sinValor: string[] }>
  obtenerPresupuesto(desde: string, hasta: string): Promise<Presupuesto>
  ```

- [ ] **Step 1: Escribir las pruebas (fallan: no existe el módulo).** `parsearHoras`/`mensajeDeHoras` (`""` → null; `"7,5"`, `"7.5"`, `"0"`, `"24"`, `"0,25"` valen; `"24,5"`, `"-1"`, `"0,1"`, `"abc"` y `"1,2,3"` son inválidas con el mensaje «Las horas van de 0 a 24, de a cuarto de hora.»); `cambiosDelBorrador` (sin cambios → `[]`; cambiar horas de un día cargado → un cambio; cargar un día sin registro → un cambio; vaciar un día cargado → `horas: null`; cambiar solo la nota; una nota de espacios → `null`; un texto inválido no se envía); `conDiaNuevo` (agrega una vez; repetido no duplica; `programadas: 0`); `resumenPorEntrenador` (suma lo cargado según el borrador, no según el original); `rangoDelMes("2026-02")` → `2026-02-01` a `2026-02-28`, `"2028-02"` → 29, `"2026-12"` → 31; `nombreDelDia("2026-11-09")` → `lun`; `formatoDeHoras`; `formatoDeImporte(1234.5)` → `$ 1.234,50` (reemplazando el espacio duro por uno común antes de comparar).
- [ ] **Step 2: Correr y ver el fallo.** `node --experimental-strip-types --test tests/horas.test.ts` desde `frontend/` (PowerShell) → falla por módulo inexistente.
- [ ] **Step 3: Implementar** `horasUtil.ts` y `horasApi.ts` (mismo `pedir` y `mensajeDeError` que `calendarioApi.ts`); sumar `valorHora` a `Entrenador` (calendarioUtil) y a `DatosDeEntrenador` (`guardarEntrenador` lo envía como número o `null` si está vacío). Ajustar la ficha de prueba de `calendario.test.ts` y `entrenadores.test.ts`.
- [ ] **Step 4: Correr y ver que pasan.** Todas las pruebas del frontend y `npm run build`.
- [ ] **Step 5: Commit.** `git add frontend` · `Horas de entrenadores (pantalla): lógica del período y clientes de la API`.

---

## Task 5: Pantallas: Horas, Presupuesto, PDF y valor de la hora

**Files:**
- Create: `frontend/src/features/entrenadores/PestanaHoras.tsx`, `frontend/src/features/entrenadores/PestanaPresupuesto.tsx`, `frontend/src/utils/generarPresupuestoPDF.ts`
- Modify: `frontend/src/features/entrenadores/ModuloEntrenadores.tsx`, `frontend/src/features/entrenadores/PestanaEntrenadores.tsx` (columna **Valor de la hora**), `frontend/src/features/entrenadores/ModalEntrenador.tsx` (campo **Valor de la hora**), `frontend/src/features/manual/secciones.tsx`

**Interfaces:**
- Consumes: Task 4; `useAuth().user.rol`; `pdfComun` (`encabezadosYPies`, `estiloTabla`, `abrirPDF`, `importe`, `pesos`, `fecha`, `ALTO_ENCABEZADO`, `MARGEN`).
- Produces: `generarPresupuestoPDF(p: Presupuesto): void` (A4 vertical, blanco y negro; título «Presupuesto de pago a entrenadores», período «Del dd/mm/aaaa al dd/mm/aaaa»; columnas Entrenador, Días, Horas, Valor por hora, Monto; fila de total; si alguna fila tiene `valorHora` nulo, una nota al pie: «Valor por hora «varios»: cambió dentro del período; el monto suma cada día con el valor que tenía.»; archivo `Presupuesto de pago a entrenadores <desde> al <hasta>`).

- [ ] **Step 1: Módulo con pestañas.** `ModuloEntrenadores`: para Administrador y Contador, `role="tablist"` con **Lista | Horas | Presupuesto** (flechas izquierda y derecha entre pestañas, como en el calendario que ya había); para Recepción, solo la lista y sin pestañas. Cambiar de pestaña con cambios sin guardar en Horas avisa con un aviso en pantalla («Hay cambios sin guardar. ¿Los descartás?») y no usa `window.confirm`.
- [ ] **Step 2: Horas.** `PestanaHoras`: selector `<input type="month">` (por defecto el mes actual); pide `obtenerHoras(rangoDelMes(mes))` dentro del efecto con bandera de cancelación; por entrenador, una tabla con fecha y día de la semana, «Le tocaba» (`formatoDeHoras`), «Cumplió» (`<input type="text" inputMode="decimal">`, error de `mensajeDeHoras` junto al campo) y nota; resumen por entrenador; **Agregar un día** (selector de fecha dentro del período y hasta hoy, que usa `conDiaNuevo`); **Guardar cambios** (envía `cambiosDelBorrador`; recarga y avisa con una tostada de éxito; el mensaje del servidor que nombre a un entrenador o un campo va junto a la fila); **Precargar desde el calendario** (llama a `precargarHoras`, recarga y muestra «Se cargaron N días.» y, si `sinValor` no está vacío, «Faltan valores de la hora para: …» con un enlace a la lista). Los entrenadores sin valor de hora muestran el aviso «Cargá el valor de la hora» junto a su nombre. Cambios sin guardar avisan también antes de cambiar de mes (aviso en pantalla) y antes de cerrar la página (`beforeunload`).
- [ ] **Step 3: Presupuesto y PDF.** `PestanaPresupuesto`: `desde` y `hasta` (por defecto del 1 del mes a hoy), validación de período junto a los campos (invertido o de más de 62 días), tabla Entrenador, Días, Horas, Valor de la hora (`varios` si es `null`), Monto y fila de total (`formatoDeImporte`), y **Descargar PDF** que llama a `generarPresupuestoPDF`. Sin horas en el período: «No hay horas cargadas en este período.».
- [ ] **Step 4: Valor de la hora en la ficha.** `ModalEntrenador` suma el campo **Valor de la hora** (solo si el usuario es Administrador o Contador; vacío = sin valor; error del servidor junto al campo); `PestanaEntrenadores` suma la columna **Valor de la hora** (`$ 1.500,00` o «Sin valor»), también solo para la gestión.
- [ ] **Step 5: Manual.** `secciones.tsx`, sección `entrenadores`: valor de la hora, cargar horas (precarga, corregir, agregar un día), el presupuesto y el PDF, quién ve qué; filas de permisos nuevas (horas, valor de la hora y presupuesto: Administrador sí, Contador sí, Recepción no).
- [ ] **Step 6: Verificar.** Pruebas del frontend, `npm run build` y `npx eslint` sobre los archivos tocados (sin errores nuevos respecto de `main`).
- [ ] **Step 7: Commit.** `git add frontend` · `Entrenadores: pestañas Horas y Presupuesto, PDF y valor de la hora`.

---

## Task 6: Documentación, recorrido real y cierre de la Parte B

**Files:**
- Modify: `docs/BASE_DE_DATOS.md` (columna y tabla, restricciones, disparador y 15 tablas auditadas), `docs/SEGURIDAD.md` (dinero visible solo para la gestión y ocultado en el servidor; rutas de horas solo de la gestión), `README.md` (horas y presupuesto), `docs/BACKUPS.md` (si lista las tablas que entran en la copia)

- [ ] **Step 1: Documentar** los archivos con lo que cambió.
- [ ] **Step 2: Docker aparte** (`-p entrenadoresb`, puertos 5222 / 5273 / 5533, desde una copia del worktree) y recorrido real con el navegador integrado y los tres roles: cargar el valor de la hora; armar un calendario con dos entrenadores; precargar un mes (la cantidad de días creados coincide con las horas del calendario); corregir un día a 0 (faltó) y otro a 9,5; agregar un día de fin de semana; guardar y recargar; subir el valor de la hora y comprobar que los días cargados no cambian; presupuesto con dos valores distintos y su total (hacer la cuenta a mano); descargar el PDF y mirarlo; entrenador sin valor (aviso y precarga que lo informa); Recepción: sin pestañas, sin valores; período inválido; ancho de 375 px. Corregir lo que aparezca, cada defecto con su prueba.
- [ ] **Step 3: Suite completa y revisión final.** `dotnet test backend/EstudioContable.slnx` y las pruebas del frontend en verde; revisión de toda la rama por una persona nueva con el modelo más capaz disponible, y corrección de lo importante con pruebas que fallen primero.
- [ ] **Step 4: Commit y entrega.** `git add docs README.md` · `Documentación de las horas y el presupuesto de los entrenadores`. La fusión a `main` no es parte del plan: se le ofrece al usuario y, si la aprueba, se sigue el orden de la memoria (backup con `main` corriendo, detener, fusionar, `docker compose up -d`, comprobar migraciones y datos, backup nuevo, zip).
