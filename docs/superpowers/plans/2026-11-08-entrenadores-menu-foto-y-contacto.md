# Entrenadores: menú propio, foto y contacto · Plan de implementación (Parte A de 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Un ítem **Entrenadores** en el menú, y una ficha con foto, email y WhatsApp además del teléfono y la especialidad.

**Architecture:** Módulo de pantalla nuevo `ModuloEntrenadores` que recibe la lista y el diálogo que hoy viven en Calendario. La ficha suma dos columnas (`Email`, `WhatsApp`); la foto va en una tabla aparte (`FotosDeEntrenador`, sin auditoría de filas) y se valida en el navegador y otra vez en el servidor como JPEG de verdad con tamaño acotado.

**Tech Stack:** .NET 10 / EF Core / PostgreSQL 15 (xUnit + Testcontainers), React 19 + TypeScript + Tailwind (pruebas con `node --test`).

**Spec:** `docs/superpowers/specs/2026-11-08-entrenadores-menu-horas-y-presupuesto-design.md` (sección «Parte A»). La Parte B tiene su propio plan (`2026-11-08-entrenadores-horas-y-presupuesto.md`) y se hace después de fusionar esta.

## Global Constraints

- Se trabaja **solo** en el worktree `C:\Sistema_contable_wt` (rama `entrenadores-horas-y-presupuesto`). No se edita nada en `C:\Sistema_contable`: su Docker monta `./backend` con `dotnet watch` y migraría la base real. No se toca ese Docker.
- Textos en castellano rioplatense (voseo), sin emojis. Errores del servidor: `ArgumentException` / `InvalidOperationException` (el middleware los devuelve como 400), `NotFoundException` para 404. Lectura con sesión; escritura solo `Roles.Gestion`.
- `Email`: hasta **254** caracteres, forma `algo@dominio.ext` sin espacios (`^[^@\s]+@[^@\s]+\.[^@\s]+$`), vacío = `null`. `WhatsApp`: hasta **30** caracteres, solo dígitos, espacios, `+`, `-`, `(` y `)`; cualquier racha de espacios (también el duro) pasa a un espacio común; vacío = `null`. En el JSON los campos son `email` y `whatsApp`.
- Foto: **JPEG de verdad** (empieza con `FF D8 FF`, termina con `FF D9`, cabecera de tamaño `SOF` encontrada), de **100 a 150 000 bytes**, ancho y alto de **16 a 512**. El navegador la entrega de **256 × 256**, calidad 0,85 (baja hasta que entre en 150 000 bytes). Tabla `FotosDeEntrenador` **sin** disparador de auditoría; eventos de auditoría `entrenador` / `FOTO` y `FOTO_BAJA` (`auditoria.operacion` es `varchar(10)`: un nombre más largo falla en silencio).
- `GET api/entrenadores/{id}/foto`: `image/jpeg`, `X-Content-Type-Options: nosniff`, `Cache-Control: private, no-cache`, `ETag` derivado de `Actualizada`.
- Frontend: sin errores de lint nuevos (usar la forma «pedir dentro del efecto con `.then` y bandera de cancelación», no una función que cambie estado llamada desde el efecto); no abrir `<form>` dentro de otro. Pruebas del frontend con `node --experimental-strip-types --test tests/*.test.ts` desde `frontend/` (el `npm test` falla en Git Bash).
- Trampas del entorno: en heredocs de Python por la herramienta Bash, `\\` llega como `\`, `\u00A0` se vuelve un carácter real y `\00` un NUL: armar las barras con `chr(92)` o escribir con Edit/Write. Probar la API con Python, no con `curl` (acentos).
- Cada tarea termina con la suite completa en verde (`dotnet test backend/EstudioContable.slnx` y las del frontend). Commits con el trailer `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.

## Review Focus

- Un archivo que se hace pasar por foto (PNG con nombre `.jpg`, SVG, HTML, JPEG truncado, JPEG de 20 000 × 20 000 píxeles, cuerpo vacío, más de 150 000 bytes) se rechaza con 400 y no se guarda nada (Task 2).
- Reemplazar la foto: nadie sigue viendo la vieja por caché; el `ETag` cambia y el navegador siempre revalida (Task 2).
- Pegar el WhatsApp desde WhatsApp o Word trae espacios duros: se guarda limpio y no se rechaza (Tasks 1 y 2).
- Un entrenador sin foto, o cuya foto falla al cargar, se ve con sus iniciales y no rompe la lista (Task 4).
- La foto entra en la copia de seguridad y vuelve igual al restaurarla (Task 2).

---

## Task 1: Dominio: contacto de la ficha y reglas de la foto

**Files:**
- Modify: `backend/src/EstudioContable.Domain/Entities/Entrenador.cs`
- Create: `backend/src/EstudioContable.Domain/Entities/FotoDeEntrenador.cs`, `backend/src/EstudioContable.Domain/ReglasDeFoto.cs`
- Test: `backend/tests/EstudioContable.Domain.Tests/EntrenadorTests.cs` (agregar), `backend/tests/EstudioContable.Domain.Tests/ReglasDeFotoTests.cs`, `backend/tests/EstudioContable.Domain.Tests/JpegDePrueba.cs`

**Interfaces:**
- Produces:
  ```csharp
  // Entrenador (los parámetros nuevos son opcionales: lo que ya llama a estos métodos sigue compilando)
  public const int LargoMaximoDelEmail = 254, LargoMaximoDelWhatsApp = 30;
  public string? Email { get; private set; }
  public string? WhatsApp { get; private set; }
  public Entrenador(string? nombreCompleto, string? telefono, string? especialidad, string? email = null, string? whatsApp = null);
  public void ActualizarDatos(string? nombreCompleto, string? telefono, string? especialidad, string? email = null, string? whatsApp = null);
  public static string? LimpiarEmail(string? email);
  public static string? LimpiarWhatsApp(string? whatsApp);

  public static class ReglasDeFoto {
      public const int BytesMinimos = 100, BytesMaximos = 150_000, LadoMinimo = 16, LadoMaximo = 512;
      public static void Validar(byte[]? foto);                       // ArgumentException con mensaje claro
      public static (int Ancho, int Alto)? TamanoDelJpeg(byte[] jpeg); // null si no hay cabecera SOF
  }
  public class FotoDeEntrenador {
      public long EntrenadorId { get; private set; }  public byte[] Foto { get; private set; }  public DateTimeOffset Actualizada { get; private set; }
      protected FotoDeEntrenador() { }
      public FotoDeEntrenador(long entrenadorId, byte[] foto, DateTimeOffset ahora);   // valida con ReglasDeFoto
      public void Reemplazar(byte[] foto, DateTimeOffset ahora);                        // valida; no cambia nada si falla
  }
  ```
- `JpegDePrueba.Crear(int ancho, int alto, int bytesTotales = 400, byte marcadorSof = 0xC0) : byte[]` (solo de pruebas): `FF D8`, un segmento `COM` de relleno hasta llegar a `bytesTotales`, el `SOF` con ese ancho y alto, `FF D9`. No es una imagen que se pueda abrir; alcanza para la validación.

- [ ] **Step 1: Escribir las pruebas (fallan por compilación).** `EntrenadorTests`: `Email_y_WhatsApp_son_opcionales_y_vacio_es_null`; `El_email_se_recorta_y_tiene_que_tener_forma_de_email` (`"  ana@gym.com "` → `"ana@gym.com"`; inválidos: `"sin-arroba"`, `"a b@c.com"`, `"a@b"`, `"@c.com"`, 255 caracteres); `El_WhatsApp_pegado_con_espacios_duros_se_guarda_con_espacios_comunes` (`"+54\u00A011\u00A05555-1234"` → `"+54 11 5555-1234"`; también `\u202F` y rachas); `El_WhatsApp_solo_admite_digitos_espacios_y_signos` (`"abc"`, `"11 5555-1234 int. 5"`, `"11.5555.1234"` y 31 caracteres lanzan; `"(011) 15 5555-1234"` vale); `ActualizarDatos_con_un_email_invalido_no_cambia_nada`. `ReglasDeFotoTests`: un JPEG de 256 × 256 y 400 bytes vale; también uno progresivo (`SOF2`, `0xC2`); lanzan: `null`, vacío, 99 y 150 001 bytes, firma `89 50 4E 47` (PNG), texto SVG y HTML, JPEG sin `FF D9` al final, JPEG sin `SOF`, `DHT` (`0xC4`) y `DAC` (`0xCC`) no cuentan como `SOF`, ancho o alto de 15, de 513 y de 20 000; `TamanoDelJpeg` devuelve el ancho y el alto leídos. `FotoDeEntrenadorTests` (en el mismo archivo): `Reemplazar` cambia los bytes y la fecha; con un archivo inválido deja todo como estaba.
- [ ] **Step 2: Correr y ver el fallo.** `dotnet test backend/tests/EstudioContable.Domain.Tests --filter "FullyQualifiedName~EntrenadorTests|FullyQualifiedName~ReglasDeFoto|FullyQualifiedName~FotoDeEntrenador"` → no compila.
- [ ] **Step 3: Implementar.** `LimpiarWhatsApp` reutiliza la misma regla de espacios que `LimpiarNombre` (extraerla a un miembro compartido) y después valida con `^[0-9 +\-()]+$`. `LimpiarEmail`: recorta, rechaza espacios adentro y controla la forma con una expresión regular con tiempo límite. `ReglasDeFoto.TamanoDelJpeg`: recorre los segmentos desde el byte 2 (`FF` + marcador + largo de 2 bytes; los marcadores sin largo —`D0`–`D7`, `01`, `D8`, `D9`— no se saltan), y se detiene en el primer `SOF` (`C0`–`C3`, `C5`–`C7`, `C9`–`CB`, `CD`–`CF`), de donde lee alto y ancho; si el recorrido se sale del archivo devuelve `null`.
- [ ] **Step 4: Correr y ver que pasan.** El comando del Step 2, más `dotnet test backend/tests/EstudioContable.Domain.Tests` completo.
- [ ] **Step 5: Commit.** `git add backend` · `Entrenador: email, WhatsApp y reglas de la foto (dominio)`.

---

## Task 2: Datos y API en el servidor

**Files:**
- Modify: `backend/src/EstudioContable.Infrastructure/AppDbContext.cs` (mapeo de `Entrenador`, líneas ≈ 175-190), `backend/src/EstudioContable.Application/Interfaces/IAppDbContext.cs`, `backend/src/EstudioContable.Application/Copias/CopiasDeSeguridad.cs` (etiqueta), `backend/src/EstudioContable.API/Controllers/EntrenadoresController.cs`
- Create: migración `FichaDeEntrenadorConContacto` en `backend/src/EstudioContable.Infrastructure/Migrations/` (generada con `dotnet ef` y completada a mano), `backend/tests/EstudioContable.IntegrationTests/FotosDeEntrenadorTests.cs`, `backend/tests/EstudioContable.IntegrationTests/JpegDePrueba.cs` (copia idéntica a la de Domain.Tests), `backend/tests/EstudioContable.IntegrationTests/MigracionDeFichaTests.cs`
- Modify (pruebas): `backend/tests/EstudioContable.IntegrationTests/EntrenadoresTests.cs`

**Interfaces:**
- Consumes: Task 1.
- Produces:
  ```csharp
  public record EntrenadorDto(long Id, string Nombre, string? Telefono, string? Especialidad, string? Email, string? WhatsApp,
      bool Activo, int HorasSemanales, bool TieneFoto, DateTimeOffset? FotoActualizada);
  public record GuardarEntrenadorRequest(string? Nombre, string? Telefono, string? Especialidad, string? Email, string? WhatsApp);
  // PUT    api/entrenadores/{id:long}/foto  [Gestion] [RequestSizeLimit(160_000)]  cuerpo crudo → 204
  // DELETE api/entrenadores/{id:long}/foto  [Gestion]  → 204 (también si no tenía)
  // GET    api/entrenadores/{id:long}/foto  [cualquier sesión] → image/jpeg; 404 si no tiene; ETag "<Actualizada.UtcTicks>" y If-None-Match → 304
  DbSet<FotoDeEntrenador> FotosDeEntrenador { get; }   // IAppDbContext y AppDbContext
  ```
- Produces (base): columnas `Entrenadores."Email"` `varchar(254)` y `"WhatsApp"` `varchar(30)` nulas, `CK_Entrenadores_Email` (`"Email" IS NULL OR "Email" ~ '^[^@\s]+@[^@\s]+\.[^@\s]+$'`); tabla `contabilidad."FotosDeEntrenador"` (`EntrenadorId` clave primaria y clave foránea `ON DELETE CASCADE`, `Foto` `bytea` no nulo, `Actualizada` `timestamptz`, `CK_FotosDeEntrenador_Tamano`: `octet_length("Foto") BETWEEN 100 AND 150000`), **sin** disparador de auditoría. Etiqueta de la copia: `["contabilidad.FotosDeEntrenador"] = ("Fotos de los entrenadores", "Gimnasio", 60)`.

- [ ] **Step 1: Escribir las pruebas (fallan).** `FotosDeEntrenadorTests`: `Todos_los_roles_ven_la_foto_y_solo_la_gestion_la_sube_y_la_quita` (Recepción 403 al subir y quitar, anónimo 401, los tres roles leen; 404 si no tiene); `Se_sube_se_reemplaza_se_ve_y_se_quita_la_foto` (los bytes que se leen son los subidos; `Content-Type` `image/jpeg`; `X-Content-Type-Options: nosniff`; `Cache-Control` con `no-cache`; el `ETag` cambia al reemplazar y un `If-None-Match` con el vigente da 304; quitar dos veces da 204); `Un_archivo_que_no_es_un_JPEG_valido_se_rechaza_con_400_y_no_se_guarda` (teoría con PNG, SVG, HTML, JPEG truncado, JPEG de 20 000 × 20 000, JPEG de 15 × 15, cuerpo vacío y 150 001 bytes; después, `tieneFoto` sigue como estaba; un cuerpo de 200 000 bytes da 400 o 413); `La_lista_no_trae_los_bytes_pero_dice_si_hay_foto` (`tieneFoto`, `fotoActualizada`, y la respuesta de la lista pesa muy poco); `Subir_y_quitar_la_foto_quedan_en_la_auditoria` (`/api/auditoria?tabla=entrenador`: `FOTO` y `FOTO_BAJA` con el email de quien actuó); `La_foto_entra_en_la_copia_de_seguridad_y_vuelve_igual_al_restaurarla` (la copia `.zip` trae `datos/contabilidad.FotosDeEntrenador.csv`, `comparar` la da `igual` con etiqueta «Fotos de los entrenadores»; se sube una foto, se descarga la copia, se quita la foto, se restaura con el patrón de `CopiasDeSeguridadTests` y los bytes que se leen son los mismos); `La_base_rechaza_fotos_fuera_de_tamano_aunque_se_escriba_directo` (99 y 150 001 bytes → `CK_FotosDeEntrenador_Tamano`; `CK_Entrenadores_Email` con `"sin-arroba"`). `EntrenadoresTests` (actualizar y agregar): el alta y la edición guardan `email` y `whatsApp` limpios (`"+54\u00A011\u00A05555-1234"` queda con espacios comunes); `Email_y_WhatsApp_invalidos_dan_400_y_no_cambian_nada` (teoría: `"sin-arroba"`, `"a b@c.com"`, 255 caracteres, `"abc"`, 31 caracteres). `MigracionDeFichaTests`: con `BasesDePrueba`, migrar hasta `20261108020654_CopiasAutomaticas`, crear un entrenador, migrar a la última (las columnas nuevas son `null`, la tabla de fotos existe y está vacía), volver atrás (las columnas y la tabla ya no están y el entrenador sigue), y subir de nuevo sin error.
- [ ] **Step 2: Correr y ver el fallo.** `dotnet test backend/tests/EstudioContable.IntegrationTests --filter "FullyQualifiedName~FotosDeEntrenadorTests|FullyQualifiedName~EntrenadoresTests|FullyQualifiedName~MigracionDeFicha"` → fallan (404 y columnas inexistentes).
- [ ] **Step 3: Persistencia y migración.** Mapear `Email` y `WhatsApp` (largos y `CK_Entrenadores_Email`), `FotoDeEntrenador` (clave `EntrenadorId`, `HasOne<Entrenador>().WithMany().HasForeignKey(f => f.EntrenadorId).OnDelete(Cascade)`, `CK_FotosDeEntrenador_Tamano`) y el `DbSet`. `dotnet ef migrations add FichaDeEntrenadorConContacto --project backend/src/EstudioContable.Infrastructure --startup-project backend/src/EstudioContable.API`; revisar que no pierda datos (solo agrega) y que el `Down` sea simétrico. Agregar la etiqueta.
- [ ] **Step 4: Controlador.** `EntrenadoresController` suma `IRegistroDeEventos` y `IUsuarioActual`. `Listar` arma `TieneFoto` y `FotoActualizada` con una consulta aparte que proyecta solo `EntrenadorId` y `Actualizada` (nunca `Foto`). Alta y edición pasan `Email` y `WhatsApp` a la entidad. `PUT foto`: leer `Request.Body` a memoria, `ReglasDeFoto.Validar`, insertar o `Reemplazar`, registrar `FOTO`. `DELETE foto`: borrar si existe y registrar `FOTO_BAJA`. `GET foto`: 404 si no hay; `Response.Headers` con `nosniff` y `private, no-cache`; `File(bytes, "image/jpeg", lastModified: f.Actualizada, entityTag: ...)`. No encontrado el entrenador → `NotFoundException`.
- [ ] **Step 5: Correr y ver que pasan.** El comando del Step 2; `dotnet ef migrations has-pending-model-changes ...` sin cambios pendientes; después la suite completa `dotnet test backend/EstudioContable.slnx`.
- [ ] **Step 6: Commit.** `git add backend` · `Entrenadores: email, WhatsApp y foto (migración, API y copia de seguridad)`.

---

## Task 3: Frontend: lógica y clientes de la API

**Files:**
- Modify: `frontend/src/features/calendario/calendarioUtil.ts` (interfaz `Entrenador`), `frontend/src/api/entrenadoresApi.ts`, `frontend/src/utils/whatsapp.ts`
- Create: `frontend/src/features/entrenadores/entrenadoresUtil.ts`
- Test: `frontend/tests/entrenadores.test.ts`, `frontend/tests/whatsapp.test.ts` (agregar), `frontend/tests/calendario.test.ts` (ajustar la ficha de prueba)

**Interfaces:**
- Produces:
  ```ts
  // calendarioUtil.ts — Entrenador suma:
  email: string | null; whatsApp: string | null; tieneFoto: boolean; fotoActualizada: string | null
  // entrenadoresApi.ts
  export interface DatosDeEntrenador { id?: number; nombre: string; telefono: string; especialidad: string; email: string; whatsApp: string }
  export async function guardarEntrenador(d: DatosDeEntrenador): Promise<number>      // devuelve el id (el del alta o el de la edición)
  export async function subirFoto(id: number, foto: Blob): Promise<void>               // PUT crudo image/jpeg
  export async function quitarFoto(id: number): Promise<void>
  export async function obtenerFoto(id: number): Promise<Blob | null>                  // null si no tiene (404)
  // whatsapp.ts
  export function enlaceWhatsApp(telefono: string | null | undefined, mensaje = ''): string | null   // sin mensaje, sin «?text=»
  // entrenadoresUtil.ts (sin dependencias)
  export const LADO_DE_LA_FOTO = 256, BYTES_MAXIMOS_DE_LA_FOTO = 150_000
  export function recorteCuadrado(ancho: number, alto: number): { x: number; y: number; lado: number }
  export async function codificarHastaQueEntre(codificar: (calidad: number) => Promise<{ size: number }>): Promise<{ size: number }>   // prueba 0,85; 0,75; … 0,35; lanza Error si ninguna entra
  export function iniciales(nombre: string): string
  export function enlaceDeEmail(email: string | null | undefined): string | null           // `mailto:` solo si tiene forma de email
  export function enlaceDeWhatsAppDeEntrenador(whatsApp: string | null | undefined): string | null
  ```

- [ ] **Step 1: Escribir las pruebas (fallan).** `entrenadores.test.ts`: `recorteCuadrado` para una foto apaisada (1600 × 900 → `x = 350, y = 0, lado = 900`), vertical (900 × 1600 → `x = 0, y = 350`), cuadrada y con un lado impar (sin decimales); `codificarHastaQueEntre` devuelve la primera calidad que entra, llama al codificador con 0,85 primero y con una calidad menor después, y lanza `Error` con «no se pudo achicar» si ninguna entra; `iniciales` (`"Ana López"` → `"AL"`, `"madonna"` → `"M"`, `"  ana   maría  lópez "` → `"AM"`, `""` → `""`); `enlaceDeEmail` (válido → `mailto:ana@gym.com`; `"sin-arroba"`, `""` y `null` → `null`); `enlaceDeWhatsAppDeEntrenador` (`"11 5555-1234"` → `https://wa.me/5491155551234`; `"abc"`, `null` → `null`). `whatsapp.test.ts`: `enlaceWhatsApp("11 5555-1234")` sin mensaje no lleva `?text=`; con mensaje sigue igual que hoy.
- [ ] **Step 2: Correr y ver el fallo.** PowerShell: `cd frontend; node --experimental-strip-types --test tests/entrenadores.test.ts tests/whatsapp.test.ts` → falla por exports inexistentes.
- [ ] **Step 3: Implementar.** `entrenadoresApi.ts`: `guardarEntrenador` devuelve el `id` del `POST` (`201 { id }`) o el recibido en la edición; `subirFoto` envía `body: foto` con `Content-Type: image/jpeg`; `obtenerFoto` devuelve `null` en 404 y lanza en otros errores. Actualizar la interfaz `Entrenador` y la ficha de prueba de `calendario.test.ts`.
- [ ] **Step 4: Correr y ver que pasan.** Todas las pruebas del frontend (`node --experimental-strip-types --test tests/*.test.ts` desde `frontend/`) y `npm run build` sin errores de tipos (el diálogo todavía llama a `guardarEntrenador` y no usa el valor devuelto).
- [ ] **Step 5: Commit.** `git add frontend` · `Entrenadores (pantalla): lógica de la foto y del contacto y clientes de la API`.

---

## Task 4: Pantallas y menú

**Files:**
- Create: `frontend/src/features/entrenadores/ModuloEntrenadores.tsx`, `frontend/src/features/entrenadores/AvatarDeEntrenador.tsx`, `frontend/src/features/entrenadores/procesarFoto.ts`
- Move (con `git mv`): `features/calendario/PestanaEntrenadores.tsx` y `features/calendario/ModalEntrenador.tsx` → `features/entrenadores/`
- Modify: `frontend/src/features/calendario/ModuloCalendario.tsx`, `frontend/src/App.tsx` (importación de íconos ≈ líneas 4-9, menú ≈ línea 82, `renderVista` ≈ línea 212), `frontend/src/features/manual/indice.ts`, `frontend/src/features/manual/secciones.tsx`, `frontend/src/features/calendario/calendarioUtil.ts` y `frontend/tests/calendario.test.ts` (quitar `conHorasDelCalendario` y su prueba: pasa a ser código sin uso)

**Interfaces:**
- Consumes: Task 3 (`obtenerFoto`, `subirFoto`, `quitarFoto`, `guardarEntrenador`, `iniciales`, `recorteCuadrado`, `codificarHastaQueEntre`, `enlaceDeEmail`, `enlaceDeWhatsAppDeEntrenador`).
- Produces:
  ```ts
  export function ModuloEntrenadores(): JSX.Element                                    // la lista; la Parte B le suma las pestañas Horas y Presupuesto
  export function AvatarDeEntrenador(p: { entrenador: Pick<Entrenador, 'id' | 'nombre' | 'tieneFoto' | 'fotoActualizada'>; tamano?: number }): JSX.Element
  export async function procesarFoto(archivo: File): Promise<Blob>                     // recorta, achica a 256 × 256 y codifica a JPEG (solo navegador); lanza Error con mensaje claro si no es una imagen que se pueda abrir
  // ModuloCalendario({ onIrAEntrenadores }: { onIrAEntrenadores: () => void })
  ```

- [ ] **Step 1: Menú y módulo.** En `App.tsx`: ícono `Contact` de `lucide-react` (si no compila, otro que exista), vista `{ id: 'entrenadores', titulo: 'Entrenadores', icono: <Contact className={icono} /> }` en el grupo *Recepción* a continuación de `calendario`, sin `roles`; `case 'entrenadores': return <ModuloEntrenadores />` y `<ModuloCalendario onIrAEntrenadores={() => irA('entrenadores')} />`. `ModuloEntrenadores` carga la lista (`obtenerEntrenadores`) con el patrón de lint de las Global Constraints, muestra `PestanaEntrenadores` y recarga tras cada alta, edición o cambio de estado. Sin pestañas todavía.
- [ ] **Step 2: Calendario sin pestañas.** Quitar de `ModuloCalendario` el juego de pestañas, `PestanaEntrenadores`, `alCambiarUnEntrenador`, `entrenadoresConHoras` y el aviso de «cambios sin guardar» de esa pestaña; el módulo sigue pidiendo la lista de entrenadores para la grilla. El botón **Ir a Entrenadores** del aviso llama a `onIrAEntrenadores`.
- [ ] **Step 3: Lista y avatar.** `AvatarDeEntrenador`: pide la foto con `obtenerFoto` dentro de un efecto con bandera de cancelación, la muestra con un `URL.createObjectURL` que se libera al desmontar (caché por `id` y `fotoActualizada`); sin foto, con error o mientras carga, un círculo con `iniciales`. `PestanaEntrenadores` suma la foto, **WhatsApp** (botón que abre `enlaceDeWhatsAppDeEntrenador` en otra pestaña; sin enlace posible, el texto sin botón) y **email** (`mailto:`); sigue mostrando teléfono, especialidad, horas por semana y estado.
- [ ] **Step 4: Diálogo.** `ModalEntrenador` suma `email`, `whatsApp` y el selector de foto (`accept="image/*"`, vista previa, **Quitar foto**), con errores junto a cada campo (el mensaje del servidor que nombre «email» o «WhatsApp» va junto a ese campo). Al guardar: `guardarEntrenador`, que devuelve el id, y después `subirFoto` o `quitarFoto` si cambió. Si la foto falla después de guardar los datos, el diálogo queda abierto en modo edición de ese entrenador (guardar el `id` recibido) con el motivo junto al selector: «Se guardaron los datos, pero la foto no se pudo subir: …». `procesarFoto`: `createImageBitmap(archivo)`, recorte de `recorteCuadrado`, canvas de 256 × 256, `codificarHastaQueEntre` sobre `canvas.toBlob(…, 'image/jpeg', calidad)`.
- [ ] **Step 5: Manual.** `indice.ts`: `{ id: 'entrenadores', titulo: 'Entrenadores' }` después de `calendario`; `secciones.tsx`: sección `entrenadores` (cómo llegar, cargar la ficha con foto, email y WhatsApp, el botón de WhatsApp, quién puede qué) y recorte de la subsección «Cargar a los entrenadores» de `calendario` (queda un párrafo que manda al ítem Entrenadores); filas de permisos actualizadas.
- [ ] **Step 6: Verificar.** Pruebas del frontend (`tests/manual.test.ts` comprueba que cada id tenga sección), `npm run build` y `npx eslint` sobre los archivos tocados (sin errores nuevos respecto de `main`).
- [ ] **Step 7: Commit.** `git add frontend` · `Entrenadores: ítem propio en el menú, foto, email y WhatsApp en la ficha`.

---

## Task 5: Documentación, recorrido real y cierre de la Parte A

**Files:**
- Modify: `docs/BASE_DE_DATOS.md` (columnas y tabla nuevas, restricciones; la tabla de fotos sin disparador de auditoría), `docs/SEGURIDAD.md` (foto: validación en dos lugares, tope de bytes y de píxeles, `nosniff`, tabla sin auditoría), `README.md` (sección «Calendario de entrenadores»: ítem propio, foto y contacto)

- [ ] **Step 1: Documentar** los tres archivos con lo que cambió.
- [ ] **Step 2: Docker aparte** (`-p entrenadoresa`, puertos 5222 / 5273 / 5533, desde una copia del worktree, como la vez anterior) y recorrido real en el navegador integrado como Administrador, Contador y Recepción: el ítem en el menú; alta con foto (como el navegador integrado no elige archivos, armar un JPEG en un canvas con `javascript_tool` y ponerlo en el `<input type="file">` con un `DataTransfer`), email y WhatsApp; la foto y las iniciales; una foto que no se puede cargar (borrar su fila en la base y recargar, o pedir una foto de un entrenador que ya no la tiene) se ve con las iniciales y no rompe la lista; el botón de WhatsApp; reemplazar y quitar la foto; un archivo que no es una imagen y un JPEG de más de 150 KB (rechazo claro junto al campo); Recepción ve todo y no tiene botones de edición; el aviso del Calendario sin entrenadores lleva al ítem; ancho de 375 px. Registrar y corregir lo que aparezca, cada defecto con su prueba.
- [ ] **Step 3: Suite completa y revisión final.** `dotnet test backend/EstudioContable.slnx` y las pruebas del frontend en verde; revisión de toda la rama por una persona nueva con el modelo más capaz disponible, y corrección de lo importante con pruebas que fallen primero.
- [ ] **Step 4: Commit y entrega.** `git add docs README.md` · `Documentación de la ficha del entrenador`. La fusión a `main` no es parte del plan: se le ofrece al usuario con las opciones habituales y, si la aprueba, se sigue el orden de la memoria (backup con `main` corriendo, detener backend y frontend, fusionar, `docker compose up -d`, comprobar las migraciones y los datos, backup nuevo, zip).
