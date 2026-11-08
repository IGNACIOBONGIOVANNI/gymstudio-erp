# Entrenadores en el calendario · Plan de implementación (Parte 1 de 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cargar entrenadores como fichas y que cada turno del calendario apunte a un entrenador activo (una persona por hora), en vez de texto libre.

**Architecture:** Entidad nueva `Entrenador` y clave foránea `TurnoEncargado.EntrenadorId` que reemplaza la columna de texto, con una sola migración que convierte los nombres ya cargados en fichas. API nueva `api/entrenadores`; `api/calendario` pasa a hablar de `entrenadorId`. En la pantalla, el Calendario gana una pestaña **Entrenadores** y la grilla valida contra la lista.

**Tech Stack:** .NET 10 / EF Core / PostgreSQL 15 (xUnit + Testcontainers), React 19 + TypeScript + Tailwind (pruebas con `node --test`).

**Spec:** `docs/superpowers/specs/2026-11-07-entrenadores-y-copias-automaticas-design.md` (sección «Parte 1»). Esta es la Parte 1; la Parte 2 tiene su propio plan (`2026-11-07-copias-automaticas.md`) y se hace después.

## Global Constraints

- Textos de pantalla y mensajes de error en castellano rioplatense (voseo), sin emojis. Errores del servidor: `ArgumentException` o `InvalidOperationException` (el middleware los devuelve como 400), `NotFoundException` para 404.
- Permisos: leer entrenadores y calendario, cualquier usuario con sesión; escribir, solo `Roles.Gestion` (Administrador y Contador).
- Nombre del entrenador: obligatorio, hasta **80** caracteres, sin caracteres de control, espacios repetidos reducidos a uno, **único sin distinguir mayúsculas** (índice único en la base). Teléfono hasta **30**, especialidad hasta **60**, ambos opcionales (vacío = `null`).
- Calendario sin cambios de forma: lunes a viernes (1–5), horas 6–21, una persona por celda, 80 celdas como máximo, índice `ux_turnos_encargado_dia_hora`.
- Las reglas de `ReglasDeCalendario.cs` y `frontend/src/features/calendario/calendarioUtil.ts` deben seguir coincidiendo.
- En pantalla, «encargado» pasa a «entrenador a cargo»; los nombres internos del código (`encargado`, `Grilla`, `TurnoEncargado`) no se renombran.
- Pruebas de integración necesitan Docker (Testcontainers). `npm test` se corre desde PowerShell: falla en Git Bash.
- **Docker del usuario:** `./backend` está montado en su contenedor con `dotnet watch`, que aplica migraciones al arrancar. Antes de empezar: `docker compose stop backend frontend` (la base queda arriba). Al final de la Parte 1: `scripts\backup.ps1` y recién entonces `docker compose up -d`. Tras compilar en Windows, el watch queda roto: reiniciar el backend.
- Commits con el trailer `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- Desvío menor del spec: activar/desactivar es `PUT /api/entrenadores/{id}/estado` con `{ "activo": bool }`, igual que `UsuariosController`, en lugar de dos `POST`.

## Review Focus

- El mismo nombre con otra grafía («Ana López», «  ana   LÓPEZ ») es una sola ficha: lo fijan el alta (Task 3), la migración (Task 2) y la coincidencia de la grilla (Task 4).
- Alguien desactiva a un entrenador mientras otra persona edita el calendario y guarda: el servidor rechaza con 400 y no cambia nada (Task 2).
- Un entrenador renombrado se ve con el nombre nuevo en turnos que ya estaban cargados (Task 3).
- Datos viejos con nombres repetidos por mayúsculas o espacios se migran a una ficha por persona, y la migración se puede deshacer sin perder el calendario (Task 2).
- Calendario abierto para editar sin ningún entrenador cargado: avisa y lleva a la pestaña Entrenadores en vez de mostrar celdas que nunca se pueden guardar (Task 5).

---

## Task 1: Ficha del entrenador (dominio)

**Files:**
- Create: `backend/src/EstudioContable.Domain/Entities/Entrenador.cs`
- Test: `backend/tests/EstudioContable.Domain.Tests/EntrenadorTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public class Entrenador {
      public const int LargoMaximoDelNombre = 80, LargoMaximoDelTelefono = 30, LargoMaximoDeLaEspecialidad = 60;
      public long Id { get; private set; }
      public string NombreCompleto { get; private set; }
      public string? Telefono { get; private set; }
      public string? Especialidad { get; private set; }
      public bool Activo { get; private set; }            // true al crear
      public DateTimeOffset FechaCreacion { get; private set; }  // UtcNow al crear
      protected Entrenador() { }                           // EF Core
      public Entrenador(string? nombreCompleto, string? telefono, string? especialidad);
      public void ActualizarDatos(string? nombreCompleto, string? telefono, string? especialidad);
      public void Activar();  public void Desactivar();
      public static string LimpiarNombre(string? nombre);  // regla del nombre; lanza ArgumentException("El nombre del entrenador es requerido.") si queda vacío
  }
  ```

- [ ] **Step 1: Escribir `EntrenadorTests` (deben fallar por compilación).** Pruebas: `El_nombre_se_limpia_de_espacios_sobrantes_y_repetidos` (`"  María   José  "` → `"María José"`); `El_nombre_es_obligatorio_y_tiene_tope` (null, `""`, `"   "`, 81 letras y `"Ana\n"` lanzan `ArgumentException`; 80 letras es válido); `Telefono_y_especialidad_son_opcionales_y_vacio_es_null` (`"  "` → null; teléfono de 31 y especialidad de 61 lanzan); `Se_crea_activo_y_se_puede_desactivar_y_activar`; `ActualizarDatos_aplica_las_mismas_reglas`.
- [ ] **Step 2: Correr y ver el fallo.** `dotnet test backend/tests/EstudioContable.Domain.Tests --filter "FullyQualifiedName~EntrenadorTests"` → error de compilación (`Entrenador` no existe).
- [ ] **Step 3: Implementar `Entrenador`** con `ReglasDeDatos.TextoRequerido`/`TextoOpcional` para largos y caracteres de control; `LimpiarNombre` reduce `\s+` a un espacio antes de validar.
- [ ] **Step 4: Correr y ver que pasan.** Mismo comando → todas verdes.
- [ ] **Step 5: Commit.** `git add backend/src/EstudioContable.Domain/Entities/Entrenador.cs backend/tests/EstudioContable.Domain.Tests/EntrenadorTests.cs` · mensaje `Entrenador: ficha con nombre único, teléfono y especialidad`.

---

## Task 2: El turno apunta a un entrenador (datos, migración y calendario en el servidor)

**Files:**
- Modify: `backend/src/EstudioContable.Domain/Entities/TurnoEncargado.cs`, `backend/src/EstudioContable.Domain/ReglasDeCalendario.cs` (quitar `NombreDelEncargado` y `LargoMaximoDelNombre`), `backend/tests/EstudioContable.Domain.Tests/ReglasDeCalendarioTests.cs` (quitar las pruebas del nombre)
- Modify: `backend/src/EstudioContable.Application/Interfaces/IAppDbContext.cs`, `backend/src/EstudioContable.Infrastructure/AppDbContext.cs` (≈ líneas 44 y 181-191), `backend/src/EstudioContable.Application/Copias/CopiasDeSeguridad.cs` (etiquetas)
- Create: migración `EntrenadoresEnElCalendario` en `backend/src/EstudioContable.Infrastructure/Migrations/` (generada con `dotnet ef` y editada a mano)
- Modify: `backend/src/EstudioContable.API/Controllers/CalendarioController.cs`
- Modify (reescribir): `backend/tests/EstudioContable.IntegrationTests/CalendarioTests.cs`
- Create: `backend/tests/EstudioContable.IntegrationTests/MigracionDeEntrenadoresTests.cs`

**Interfaces:**
- Consumes: `Entrenador` (Task 1).
- Produces:
  ```csharp
  // TurnoEncargado
  public long EntrenadorId { get; private set; }
  public Entrenador? Entrenador { get; private set; }
  public TurnoEncargado(int diaSemana, int hora, long entrenadorId);   // valida la celda; entrenadorId > 0 o ArgumentException
  public void CambiarEntrenador(long entrenadorId);
  // IAppDbContext / AppDbContext
  DbSet<Entrenador> Entrenadores { get; }
  // Contrato de api/calendario (JSON camelCase)
  GET  → [{ "dia": 1, "hora": 6, "entrenadorId": 12, "entrenador": "Ana López" }]
  PUT  { "turnos": [{ "dia": 1, "hora": 6, "entrenadorId": 12 }] } → 200 con el mismo formato que el GET
  ```
- Produces (base): tabla `contabilidad."Entrenadores"` (`Id`, `NombreCompleto` varchar(80), `Telefono` varchar(30) null, `Especialidad` varchar(60) null, `Activo`, `FechaCreacion`), `CK_Entrenadores_Nombre` (`char_length(btrim("NombreCompleto")) > 0`), índice único `ux_entrenadores_nombre` sobre `lower("NombreCompleto")`, trigger `trg_auditar_entrenadores` (`fn_auditar`); `TurnosEncargado."EntrenadorId"` `NOT NULL`, FK `FK_TurnosEncargado_Entrenadores_EntrenadorId` con `ON DELETE RESTRICT` e índice; sin la columna `Encargado` ni `CK_TurnosEncargado_Nombre`.

- [ ] **Step 1: Escribir las pruebas que deben fallar.**
  - `CalendarioTests` (reescribir): mismo conjunto que hoy, pero con `Turno(dia, hora, entrenadorId)`, un helper `Entrenador(string nombre)` que inserta la ficha directo en la base (`INSERT ... ON CONFLICT DO NOTHING` + `SELECT "Id"`; el endpoint de entrenadores llega en la Task 3) y `Resumen` que muestra `entrenador`. Se conservan: permisos (401/403/lectura de los tres roles), guardar ordenado, calendario vacío borra todo, extremos de la semana (80 celdas), pedidos inválidos (día, hora, celda repetida, turno nulo, más de 80, **y** `entrenadorId` 0, negativo e inexistente), cuerpo roto, auditoría de solo lo que cambió, guardar lo mismo no audita, restricciones de la base (`CK_TurnosEncargado_Dia`, `_Hora`, `ux_turnos_encargado_dia_hora`), la copia incluye el calendario (etiqueta ahora `Calendario de entrenadores`). El texto hostil pasa a probarse con fichas de nombre hostil (se guardan como texto).
  - Nuevas en `CalendarioTests`: `Un_entrenador_desactivado_se_rechaza_con_400_y_no_cambia_nada` (se desactiva por SQL directo: `UPDATE ... SET "Activo" = false`); `La_base_rechaza_un_turno_de_un_entrenador_inexistente` (`23503`, `FK_TurnosEncargado_Entrenadores_EntrenadorId`); `La_base_no_deja_borrar_un_entrenador_con_turnos` (`23503`).
  - `MigracionDeEntrenadoresTests` (`[Collection(ColeccionApi.Nombre)]`): crea una base nueva `migracion_entrenadores` en el mismo contenedor (`ApiFixture.ConnectionString`, `CREATE DATABASE`), arma un `AppDbContext` contra ella y migra con `IMigrator.MigrateAsync("20261106182940_TurnosDeEncargado")`; inserta por SQL los turnos `(1,6,'Ana')`, `(1,7,'  ana  ')`, `(2,6,'Ana  López')`, `(2,7,'ana lópez')`, `(3,6,'Beto')`; migra a la última. Aserciones: `Entrenadores` tiene exactamente `Ana`, `Ana López`, `Beto` (primera grafía por día y hora, ya limpia), activos; los cinco turnos apuntan a la ficha correcta; la columna `Encargado` ya no existe. Luego `MigrateAsync("20261106182940_TurnosDeEncargado")` (hacia atrás): `Encargado` vuelve con `Ana`, `Ana`, `Ana López`, `Ana López`, `Beto` y no existe la tabla `Entrenadores`. Una segunda prueba migra una base **sin turnos** hacia arriba y hacia abajo sin error.
- [ ] **Step 2: Correr y ver que fallan.** `dotnet test backend/tests/EstudioContable.IntegrationTests --filter "FullyQualifiedName~CalendarioTests|FullyQualifiedName~MigracionDeEntrenadores"` → no compila (`EntrenadorId`, `Entrenadores`).
- [ ] **Step 3: Cambiar el dominio.** `TurnoEncargado` y `ReglasDeCalendario` según *Interfaces*; ajustar `ReglasDeCalendarioTests`.
- [ ] **Step 4: Persistencia.** `Entrenadores` en `IAppDbContext` y `AppDbContext`; mapear `Entrenador` (largos, `CK_Entrenadores_Nombre`) y `TurnoEncargado` (`HasOne(x => x.Entrenador).WithMany().HasForeignKey(x => x.EntrenadorId).OnDelete(DeleteBehavior.Restrict)`, sin el `CK_TurnosEncargado_Nombre`). Agregar a `EtiquetasDeTablas`: `["contabilidad.Entrenadores"] = ("Entrenadores", "Gimnasio", 59)` y cambiar la etiqueta de `TurnosEncargado` a `"Calendario de entrenadores"`. Agregar en `ExceptionMiddleware.TraducirErrorDeBase` el caso `"23505" when pg.ConstraintName == "ux_entrenadores_nombre"` → `"Ya hay un entrenador con ese nombre."`.
- [ ] **Step 5: Migración.** `dotnet ef migrations add EntrenadoresEnElCalendario --project backend/src/EstudioContable.Infrastructure --startup-project backend/src/EstudioContable.API`, y editar `Up`/`Down` para este orden (el generador no sabe de datos): crear `Entrenadores` y su `CK` → `CREATE UNIQUE INDEX ux_entrenadores_nombre ON contabilidad."Entrenadores" (lower("NombreCompleto"))` (el modelo de EF no lo conoce, está bien) → agregar `EntrenadorId` **nullable** → backfill → volverlo `NOT NULL` → índice y FK `Restrict` → quitar `CK_TurnosEncargado_Nombre` y la columna `Encargado` → trigger `trg_auditar_entrenadores` (después del backfill, para no auditar la carga masiva). El backfill (algoritmo que no sale de las firmas):
  ```sql
  -- nombre limpio = regexp_replace(btrim("Encargado"), '\s+', ' ', 'g')
  INSERT INTO contabilidad."Entrenadores" ("NombreCompleto", "Activo", "FechaCreacion")
  SELECT DISTINCT ON (lower(n)) n, true, now()
  FROM (SELECT regexp_replace(btrim("Encargado"), '\s+', ' ', 'g') AS n, "DiaSemana", "Hora" FROM contabilidad."TurnosEncargado") t
  ORDER BY lower(n), "DiaSemana", "Hora";
  UPDATE contabilidad."TurnosEncargado" t SET "EntrenadorId" = e."Id"
  FROM contabilidad."Entrenadores" e
  WHERE lower(e."NombreCompleto") = lower(regexp_replace(btrim(t."Encargado"), '\s+', ' ', 'g'));
  ```
  El `UPDATE` dispara el trigger de auditoría que `TurnosEncargado` ya tiene: queda un registro por turno migrado y está bien, no se desactiva. `Down` hace lo inverso: vuelve a crear `Encargado` (nullable), lo completa con `NombreCompleto` del entrenador, lo vuelve `NOT NULL`, repone `CK_TurnosEncargado_Nombre`, quita FK, índice y columna `EntrenadorId`, el trigger y la tabla.
- [ ] **Step 6: `CalendarioController`.** `TurnoDto(int Dia, int Hora, long EntrenadorId, string? Entrenador)`. `Guardar` valida celdas como hoy, junta los `EntrenadorId` distintos y rechaza con `ArgumentException("Uno de los entrenadores no existe o está desactivado.")` si alguno no existe o tiene `Activo == false`; compara por `EntrenadorId` y usa `CambiarEntrenador`. `Listar` trae el nombre con un `Include`/proyección (`t.Entrenador!.NombreCompleto`).
- [ ] **Step 7: Correr y ver que pasan.** Mismo comando del Step 2, más `dotnet test backend/tests/EstudioContable.Domain.Tests` → todo verde. Comprobar que el modelo no tiene cambios pendientes: `dotnet ef migrations has-pending-model-changes --project backend/src/EstudioContable.Infrastructure --startup-project backend/src/EstudioContable.API`.
- [ ] **Step 8: Commit.** `git add backend` · `Calendario: cada turno apunta a un entrenador (migración con datos, clave foránea y auditoría)`.

---

## Task 3: API de entrenadores

**Files:**
- Create: `backend/src/EstudioContable.API/Controllers/EntrenadoresController.cs`
- Test: `backend/tests/EstudioContable.IntegrationTests/EntrenadoresTests.cs`

**Interfaces:**
- Consumes: `Entrenador`, `IAppDbContext.Entrenadores`, `TurnosEncargado` (Task 2).
- Produces (JSON camelCase):
  ```csharp
  public record EntrenadorDto(long Id, string Nombre, string? Telefono, string? Especialidad, bool Activo, int HorasSemanales);
  public record GuardarEntrenadorRequest(string? Nombre, string? Telefono, string? Especialidad);
  public record CambiarEstadoRequest(bool Activo);
  // GET  api/entrenadores?soloActivos=false  [Authorize]            → EntrenadorDto[] por nombre
  // POST api/entrenadores                    [Gestion]  → 201 { id }
  // PUT  api/entrenadores/{id}               [Gestion]  → 204
  // PUT  api/entrenadores/{id}/estado        [Gestion]  → 204
  ```

- [ ] **Step 1: Escribir `EntrenadoresTests` (fallan: 404 en la ruta).** Pruebas: `Todos_los_roles_leen_la_lista_y_sin_sesion_no_se_ve`; `Solo_Administrador_y_Contador_cargan_editan_y_cambian_el_estado` (Recepción y anónimo: 403/401 y nada cambia); `Un_nombre_repetido_en_otra_grafia_se_rechaza` (alta de `"  ana   LÓPEZ "` con `Ana López` existente → 400 con «Ya hay un entrenador»; `PUT` al nombre de otro → 400; `PUT` con su propio nombre en otra grafía → 204); `Un_pedido_invalido_se_rechaza_con_400` (teoría: nombre nulo, vacío, en blanco, 81 letras, con `\n`; teléfono de 31; especialidad de 61); `El_texto_hostil_se_guarda_como_texto`; `Desactivar_con_horas_en_el_calendario_se_rechaza_con_la_cantidad` (mensaje contiene «3 horas» con 3 turnos, «1 hora» con uno) y el entrenador sigue activo; `Se_desactiva_sin_horas_y_se_vuelve_a_activar`; `soloActivos_oculta_a_los_desactivados`; `La_lista_trae_las_horas_semanales`; `Un_entrenador_inexistente_da_404`; `Renombrar_a_un_entrenador_se_ve_en_el_calendario` (turno ya guardado, `PUT` con nombre nuevo, `GET /api/calendario` muestra el nuevo); `La_copia_de_seguridad_incluye_a_los_entrenadores` (CSV `datos/contabilidad.Entrenadores.csv` con el nombre, y `comparar` devuelve etiqueta `Entrenadores`, grupo `Gimnasio`, estado `igual`).
- [ ] **Step 2: Correr y ver el fallo.** `dotnet test backend/tests/EstudioContable.IntegrationTests --filter "FullyQualifiedName~EntrenadoresTests"` → fallan por 404.
- [ ] **Step 3: Implementar `EntrenadoresController`** según *Interfaces*. El nombre duplicado se comprueba en la aplicación (`ToLower()` sobre el nombre ya limpio con `Entrenador.LimpiarNombre`, excluyendo el propio id) y lanza `InvalidOperationException("Ya hay un entrenador con ese nombre.")`; el índice único queda de respaldo. Desactivar cuenta sus `TurnosEncargado` y lanza `InvalidOperationException($"{nombre} tiene {n} {(n == 1 ? "hora" : "horas")} en el calendario por semana. Liberalas antes de desactivarlo.")`. No encontrado → `NotFoundException(nameof(Entrenador), id)`.
- [ ] **Step 4: Correr y ver que pasan.** Mismo comando → verde; después la suite completa `dotnet test backend/EstudioContable.slnx`.
- [ ] **Step 5: Commit.** `git add backend` · `API de entrenadores: alta, edición, activar y desactivar (solo con el calendario libre)`.

---

## Task 4: Lógica de la grilla y clientes de la API (frontend)

**Files:**
- Modify: `frontend/src/features/calendario/calendarioUtil.ts`, `frontend/src/api/calendarioApi.ts`
- Create: `frontend/src/api/entrenadoresApi.ts`
- Test: `frontend/tests/calendario.test.ts`

**Interfaces:**
- Produces (`calendarioUtil.ts`):
  ```ts
  export interface Entrenador { id: number; nombre: string; telefono: string | null; especialidad: string | null; activo: boolean; horasSemanales: number }
  export interface TurnoDelServidor { dia: number; hora: number; entrenadorId: number; entrenador: string }
  export interface TurnoParaGuardar { dia: number; hora: number; entrenadorId: number }
  export function turnoDesdeServidor(t: TurnoDelServidor): Turno                 // { dia, hora, encargado: t.entrenador }
  export function buscarEntrenador<T extends Pick<Entrenador, 'id' | 'nombre' | 'activo'>>(lista: readonly T[], texto: string): T | undefined  // solo activos; sin distinguir mayúsculas ni espacios repetidos; los acentos sí cuentan
  export function celdasSinEntrenador(grilla: Grilla, lista: readonly Entrenador[]): string[]   // nombres distintos de la grilla que no son un entrenador activo, ordenados
  export function canonizarGrilla(grilla: Grilla, lista: readonly Entrenador[]): Grilla        // cada nombre pasa a la grafía de la ficha; los que no coinciden quedan igual
  export function turnosParaGuardar(grilla: Grilla, lista: readonly Entrenador[]): TurnoParaGuardar[]  // ordenados por día y hora; lanza Error si algún nombre no coincide
  ```
  `entrenadoresApi.ts`: `obtenerEntrenadores(): Promise<Entrenador[]>`, `guardarEntrenador(datos: { id?: number; nombre: string; telefono: string; especialidad: string }): Promise<void>`, `cambiarEstadoDeEntrenador(id: number, activo: boolean): Promise<void>` (mismo `pedir`/`mensajeDeError` que `calendarioApi.ts`). `calendarioApi.ts`: `obtenerCalendario(): Promise<Turno[]>` mapea con `turnoDesdeServidor`; `guardarCalendario(turnos: TurnoParaGuardar[]): Promise<Turno[]>`.

- [ ] **Step 1: Escribir pruebas en `tests/calendario.test.ts` (fallan: no existen los exports).** `buscarEntrenador encuentra sin distinguir mayúsculas ni espacios repetidos` (`'ana   lópez'` → «Ana López»; `'Ana Lopez'` no); `buscarEntrenador ignora a los desactivados`; `celdasSinEntrenador lista cada nombre ajeno una sola vez y en orden`; `canonizarGrilla deja la grafía de la ficha y respeta lo que no coincide`; `turnosParaGuardar devuelve día, hora e id en orden` y `turnosParaGuardar lanza Error con un nombre que no es entrenador`; `turnoDesdeServidor usa el nombre del entrenador como encargado`.
- [ ] **Step 2: Correr y ver el fallo.** PowerShell: `cd frontend; npm test` → falla por exports inexistentes.
- [ ] **Step 3: Implementar** las funciones de *Interfaces* en `calendarioUtil.ts` reutilizando `limpiarNombre`; la comparación usa `limpiarNombre(x).toLocaleLowerCase('es')`. Crear `entrenadoresApi.ts` y adaptar `calendarioApi.ts`.
- [ ] **Step 4: Correr y ver que pasan.** `npm test` verde y `npm run build` sin errores de tipos (`ModuloCalendario.tsx` todavía usa `Turno`; no debe romperse porque `Turno` no cambia).
- [ ] **Step 5: Commit.** `git add frontend/src frontend/tests` · `Calendario (pantalla): lógica de coincidencia con los entrenadores y clientes de la API`.

---

## Task 5: Pantallas: pestaña Entrenadores y calendario con lista

**Files:**
- Create: `frontend/src/features/calendario/PestanaEntrenadores.tsx`, `frontend/src/features/calendario/ModalEntrenador.tsx`
- Modify: `frontend/src/features/calendario/ModuloCalendario.tsx`, `frontend/src/features/calendario/DialogoAsignarTurno.tsx`, `frontend/src/utils/generarCalendarioPDF.ts` (líneas 12, 52, 56), `frontend/src/features/manual/secciones.tsx` (permisos ≈ líneas 55-56 y sección `calendario`), `frontend/src/features/manual/indice.ts`

**Interfaces:**
- Consumes: todo lo de la Task 4; `useAuth()` para `puedeEditar` (Administrador o Contador), como hoy.
- Produces: `PestanaEntrenadores({ entrenadores, puedeEditar, alCambiar }: { entrenadores: Entrenador[]; puedeEditar: boolean; alCambiar: () => void })` y `ModalEntrenador({ entrenador, alCerrar, alGuardar }: { entrenador?: Entrenador | null; alCerrar: () => void; alGuardar: () => void })`.

- [ ] **Step 1: `ModuloCalendario`.** Dos pestañas (`role="tablist"`): **Calendario** y **Entrenadores**. Carga `obtenerEntrenadores()` junto al calendario y la pasa a las dos. En edición, cada celda mantiene su `<input>` con `list="entrenadores-activos"` y un `<datalist>` con los nombres activos; al salir de la celda (`onBlur`) el texto pasa a la grafía de la ficha (`canonizarGrilla`); una celda con un nombre sin ficha se marca con borde rojo y `aria-invalid`. «Guardar cambios» se desactiva mientras `celdasSinEntrenador(borrador, entrenadores).length > 0` y debajo se avisa «Estos nombres no son entrenadores cargados: …». Guardar envía `turnosParaGuardar`. Sin ningún entrenador activo, «Editar calendario» sigue permitido pero el aviso dice «Primero cargá a los entrenadores en la pestaña Entrenadores» con un botón que cambia de pestaña. Textos: «entrenador a cargo» donde hoy dice «encargado».
- [ ] **Step 2: `DialogoAsignarTurno`.** El campo de texto pasa a un `<select>` con los entrenadores activos (valor = nombre) y «Elegí un entrenador» como error; el resto (días, desde, hasta, pisados, «dejar libres») no cambia.
- [ ] **Step 3: `PestanaEntrenadores` y `ModalEntrenador`.** Tabla (nombre, teléfono, especialidad, horas por semana, estado) con botón **Nuevo entrenador**, **Editar** y **Desactivar/Activar** solo si `puedeEditar`. El modal sigue el patrón de `ModalUsuario.tsx`: `noValidate`, error junto a cada campo (`role="alert"`, `aria-invalid`, foco en el primero con error), el mensaje del servidor que nombre al entrenador va junto al campo nombre. Desactivar con horas muestra el mensaje del servidor en un aviso visible junto a la fila, sin tostada.
- [ ] **Step 4: PDF y manual.** `generarCalendarioPDF.ts`: título «Calendario de entrenadores» y nombre del archivo equivalente. Manual: `indice.ts` título «Calendario de entrenadores»; sección `calendario`: cómo cargar entrenadores (pestaña), que la grilla solo acepta entrenadores cargados, el autocompletado, y que desactivar pide liberar sus horas; filas de permisos «Cargar y editar entrenadores» (Administrador sí, Contador sí, Recepción no) y renombrar las dos filas del calendario.
- [ ] **Step 5: Verificar.** PowerShell: `npm test`, `npm run lint`, `npm run build` sin errores. Luego, en el Docker aparte de la Task 6, mirar en el navegador integrado a 1280 px y a 375 px: pestañas, tabla de entrenadores, alta con errores junto al campo, grilla con un nombre inválido en rojo y «Guardar» desactivado, aviso sin entrenadores, diálogo «Asignar turno» con la lista, y el PDF.
- [ ] **Step 6: Commit.** `git add frontend` · `Calendario: pestaña Entrenadores, grilla con lista y PDF de entrenadores`.

---

## Task 6: Documentación, verificación de punta a punta y entrega de la Parte 1

**Files:**
- Modify: `docs/BASE_DE_DATOS.md` (tabla `Entrenadores`; `TurnosEncargado` con `EntrenadorId`), `README.md` (menciones del calendario y de las tablas), `docs/SEGURIDAD.md` (permisos del calendario y de entrenadores, si hay tabla de permisos)

- [ ] **Step 1: Documentar** los tres archivos con lo que cambió (tabla nueva, FK, auditoría, permisos, la migración y su `Down`).
- [ ] **Step 2: Docker aparte.** Copiar `docker-compose.yml` al scratchpad con otros puertos (5222 / 5273 / 5533) y la ruta de `./backend` y `./frontend` apuntando al proyecto, levantarlo con `-p entrenadores` y cargar el ejercicio de la cátedra. La migración con datos viejos ya la prueba `MigracionDeEntrenadoresTests`; acá se verifica la pantalla.
- [ ] **Step 3: Recorrido manual** (navegador integrado, como Administrador, Contador y Recepción): cargar un entrenador, repetirlo en otra grafía (error junto al campo), asignarlo con «Asignar turno», renombrarlo y ver el cambio en el calendario, intentar desactivarlo con horas (mensaje), liberarlas y desactivarlo, descargar el PDF, descargar la copia de seguridad y compararla (tabla `Entrenadores` en «igual»).
- [ ] **Step 4: Suite completa.** `dotnet test backend/EstudioContable.slnx` y, desde PowerShell, `cd frontend; npm test; npm run lint; npm run build`: todo verde.
- [ ] **Step 5: Base del usuario.** `scripts\backup.ps1`, luego `docker compose up -d` (recrea los contenedores detenidos) y comprobar en `http://localhost:5173` que el calendario existente sigue igual con sus fichas. Bajar el Docker aparte y borrar sus volúmenes.
- [ ] **Step 6: Commit.** `git add docs README.md` · `Documentación de entrenadores y calendario`. No regenerar el zip todavía: se hace al final de la Parte 2.
