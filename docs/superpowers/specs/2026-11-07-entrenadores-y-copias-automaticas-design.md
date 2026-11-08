# Entrenadores en el calendario y copias de seguridad automáticas

Fecha: 7/11/2026 · Estado: diseño aprobado en el chat, pendiente de revisión de este documento · Defensa: 12/11/2026

Son dos partes independientes. Se construyen, prueban y commitean por separado: primero la Parte 1, después la Parte 2.

## Lo pedido

> «Agregar opciones en la parte de backups tipo copia diaria, semanal, mensual, etc., para que se haga automáticamente.
> Agregar que se pueda cargar, además del personal que usa el sistema, a entrenadores o coaches, así se puede vincular al calendario.»

Aclaración del usuario: en el calendario va **una sola persona por hora**, y **solo entrenadores**: quien hace la parte contable o
administrativa no figura en el calendario porque no es coach ni entrenador personal.

## Fuera de alcance

- Que el entrenador entre al sistema (usuario, contraseña, rol).
- Vincular entrenadores con socios o rutinas.
- Más de una persona por hora, o fines de semana, en el calendario.
- Cifrar las copias, enviarlas por mail o subirlas a la nube.
- Restaurar desde la lista de copias automáticas (se descarga y se usa la restauración que ya existe).
- Más de una programación a la vez.

---

## Parte 1 · Entrenadores y calendario

### Datos

**`Entrenador`** (tabla `contabilidad."Entrenadores"`):

| Campo | Regla |
|---|---|
| `Id` | clave |
| `NombreCompleto` | obligatorio, hasta 80 caracteres (`ReglasDeDatos.TextoRequerido`), **único sin distinguir mayúsculas** (lo garantiza un índice único en la base) |
| `Telefono` | opcional, hasta 30 caracteres |
| `Especialidad` | opcional, hasta 60 caracteres |
| `Activo` | por defecto `true` |
| `FechaCreacion` | UTC |

**`TurnoEncargado`**: se reemplaza la columna de texto `Encargado` por `EntrenadorId` (clave foránea obligatoria, `ON DELETE RESTRICT`).
La unicidad `(DiaSemana, Hora)` y los rangos de día y hora no cambian. El nombre que se muestra sale de un `JOIN`: no queda ninguna
copia del nombre que se desactualice.

### Migración `EntrenadoresEnElCalendario`

1. Crea la tabla `Entrenadores` y su índice único.
2. Crea un entrenador por cada nombre distinto (sin distinguir mayúsculas ni espacios sobrantes) que ya esté en `TurnosEncargado`,
   con la primera grafía que aparezca.
3. Agrega `EntrenadorId`, lo completa por nombre, lo vuelve obligatorio, agrega la clave foránea y su índice, y quita `Encargado`.
4. `Down`: vuelve a agregar `Encargado`, lo completa desde el entrenador, quita la clave foránea y la tabla.

Una migración con datos de por medio se prueba con una base que ya tenga turnos cargados, no solo vacía.

### API

- `GET /api/entrenadores` (cualquier usuario con sesión; `?soloActivos=true`): lista con id, nombre, teléfono, especialidad, activo y
  cuántas horas semanales tiene en el calendario.
- `POST /api/entrenadores`, `PUT /api/entrenadores/{id}` y `POST /api/entrenadores/{id}/activar|desactivar`: solo Administrador y Contador
  (`Roles.Gestion`).
  - Nombre repetido: 400 con mensaje claro.
  - Desactivar a quien tiene horas en el calendario: error que dice cuántas son y que primero hay que liberarlas.
- `GET /api/calendario`: cada turno trae `dia`, `hora`, `entrenadorId` y `entrenador` (el nombre).
- `PUT /api/calendario` (solo `Gestion`): recibe `dia`, `hora` y `entrenadorId`. Rechaza un entrenador que no existe o está desactivado, una
  celda repetida y las celdas fuera de rango. Sigue guardando solo las diferencias, en una operación.

### Pantalla

- El módulo **Calendario** tiene dos pestañas: **Calendario** y **Entrenadores**. Se carga en la segunda, no en *Usuarios*, porque *Usuarios*
  es solo del Administrador y el calendario también lo edita el Contador. Recepción ve las dos pestañas y no puede modificar nada.
- **Entrenadores:** tabla con nombre, teléfono, especialidad, horas por semana y estado; alta y edición en un diálogo con los errores junto
  a cada campo (como el formulario de usuario); activar y desactivar.
- **Calendario en edición:** cada celda sigue siendo un campo de texto con autocompletado (`<datalist>`) para conservar Enter y las flechas.
  Un nombre que no coincide con un entrenador activo se marca en rojo y no deja guardar. «Asignar turno» elige de una lista. Si no hay
  entrenadores cargados, avisa y lleva a la pestaña Entrenadores.
- Los textos pasan de «encargado» a «entrenador a cargo», también en el PDF.
- `EtiquetasDeTablas`: agrega `contabilidad.Entrenadores` como «Entrenadores» (grupo Gimnasio).

### Pruebas

- Unitarias: reglas de la ficha (nombre, largos).
- Integración contra PostgreSQL real: permisos (401 sin sesión; Recepción puede leer y recibe 403 al escribir), nombre repetido en
  otra grafía, desactivar con y sin horas, calendario con entrenador inexistente o desactivado, celda repetida, migración con datos viejos
  (hacia arriba y hacia abajo), y que la copia de seguridad incluya la tabla nueva.
- Frontend: la coincidencia nombre → entrenador (mayúsculas, espacios) y el bloqueo de guardado.

---

## Parte 2 · Copias automáticas

### Datos

**`ConfiguracionDeCopias`** (tabla `contabilidad."ConfiguracionDeCopias"`, una sola fila: `Id = 1` con `CHECK`):

| Campo | Regla |
|---|---|
| `Activa` | por defecto `false` |
| `Frecuencia` | `CadaSeisHoras`, `CadaDoceHoras`, `Diaria`, `Semanal`, `Mensual` |
| `Hora`, `Minuto` | 0–23 y 0–59 (por defecto 22:00) |
| `DiaDeLaSemana` | 1 (lunes) a 7 (domingo); solo si es semanal |
| `DiaDelMes` | 1 a 28; solo si es mensual (el 29, 30 y 31 no existen todos los meses) |
| `Conservar` | 1 a 60 (por defecto 14) |
| `ActivaDesde` | UTC; instante en que se activó o se cambió la programación |
| `UltimaCopia` | UTC; última copia guardada con éxito (automática o «ahora») |
| `UltimoIntento` | UTC; último intento de copia automática, haya salido bien o mal (marca desde cuándo se reintenta tras una falla) |
| `UltimoError` | texto corto y genérico, o nulo si el último intento salió bien |

Las restricciones también están como `CHECK` en la base, no solo en la API.

### Regla de horario (función pura en `Domain`, `ReglasDeCopiasAutomaticas`)

Todas las horas se interpretan en **Argentina (UTC-3, sin horario de verano)**, igual que el resto del sistema. El reloj se inyecta
(`TimeProvider`) para poder probarlo.

- **Momentos programados:**
  - `CadaSeisHoras` y `CadaDoceHoras`: cada 6 o 12 horas a partir de `Hora:Minuto` (a las 02:00, son 02, 08, 14 y 20).
  - `Diaria`: todos los días a `Hora:Minuto`.
  - `Semanal`: el `DiaDeLaSemana` a `Hora:Minuto`.
  - `Mensual`: el `DiaDelMes` a `Hora:Minuto`.
- **`ProximaEjecucion(config, referencia)`:** el primer momento programado estrictamente posterior a la referencia.
- **`Toca(config, ahora)`:** `Activa`, y `ProximaEjecucion(config, UltimaCopia ?? ActivaDesde) <= ahora`, y, si el último intento falló, que
  hayan pasado 30 minutos desde `UltimoIntento`.

Consecuencias: si el sistema estuvo apagado durante varios momentos programados, se hace **una** copia al volver. Una copia hecha con
«Hacer una copia ahora» cuenta como última y no deja la siguiente copia programada fuera de lugar.

### Tarea en segundo plano

`CopiasAutomaticasService` (`BackgroundService`), registrado en `Program.cs` y desactivable con `Copias:TareaAutomatica = false` (las pruebas
de integración lo apagan y llaman directamente a `EjecutarSiTocaAsync(ahora)`).

- Cada minuto abre un *scope*, lee la fila de configuración y, si `Toca`, genera la copia con `ICopiasDeSeguridadService.GenerarAsync`:
  **el mismo formato firmado de hoy**, sin un segundo formato.
- Guarda en la carpeta `Copias:Carpeta` (por defecto `backups/automaticas`; en Docker, `/backups/automaticas`). Escribe primero
  `gymstudio_auto_AAAAMMDD_HHMMSS.zip.tmp` y después lo renombra, así nunca queda un archivo a medias. Al empezar cada corrida borra los
  `.tmp` que hayan quedado de un corte.
- **Retención:** después de guardar con éxito, deja las `Conservar` más nuevas entre los archivos que cumplen exactamente
  `^gymstudio_auto_\d{8}_\d{6}\.zip$` y borra el resto. Nunca toca otros archivos de la carpeta.
- **Éxito:** actualiza `UltimaCopia`, `UltimoIntento` y limpia `UltimoError`. **Falla** (carpeta sin permisos, disco lleno, base
  ocupada): guarda un mensaje genérico en `UltimoError`, el detalle va al registro del servidor y reintenta a los 30 minutos.
- Una excepción nunca puede tumbar el servicio: cada vuelta va dentro de un `try/catch`, y un semáforo evita que se pisen la corrida
  automática y «Hacer una copia ahora».
- Si la tabla de configuración todavía no existe (arranque con migraciones pendientes), espera a la vuelta siguiente.

### API (solo Administrador, como `CopiasController`)

- `GET /api/copias/programacion`: configuración, próxima copia, última copia y último error.
- `PUT /api/copias/programacion`: valida todos los campos; guardar reinicia `ActivaDesde`.
- `POST /api/copias/programacion/ahora`: hace una copia en la carpeta en el momento.
- `GET /api/copias/automaticas`: lista (nombre, tamaño, fecha) de las copias de la carpeta, de la más nueva a la más vieja.
- `GET /api/copias/automaticas/{nombre}`: descarga. El nombre se valida contra la misma expresión regular de arriba **antes** de
  armar la ruta, y la ruta final se comprueba dentro de la carpeta: no se aceptan `..`, barras ni nombres ajenos.
- **Auditoría** (tabla *Copias de seguridad*): corrida automática (usuario «sistema»), intento fallido, cambio de programación (con quién
  lo hizo) y copia hecha «ahora».

### Pantalla (*Configuración › Copia de seguridad*)

Bloque **Copia automática**: interruptor, frecuencia, hora, día (solo si corresponde), cuántas conservar, «Guardar programación»,
«Hacer una copia ahora», próxima y última copia con su resultado (el error en rojo y con texto) y la lista de copias guardadas con
descarga. Los errores de validación van junto a cada campo.

### Docker

`docker-compose.yml`: el *backend* monta `./backups:/backups` y define `Copias__Carpeta=/backups/automaticas`. Es un cambio que exige
recrear el contenedor una vez (`docker compose up -d`). La carpeta ya existe en el proyecto para las copias de los scripts.

### Seguridad

- Solo el Administrador ve, cambia y descarga.
- Descarga protegida contra recorrido de directorios (ver API).
- Las copias llevan la misma firma HMAC: una copia alterada se compara pero no se restaura.
- Siguen sin estar cifradas y tienen datos de socios y hashes de contraseñas: la carpeta es privada, igual que `backups/`.
- Como la programación vive en la base, **restaurar una copia devuelve la programación a como estaba en esa copia**. Se documenta.
- Una copia hecha antes de la Parte 1 es de otra versión de la base: se compara pero no se restaura desde la pantalla (comportamiento ya existente).

### Pruebas

- Unitarias de `ReglasDeCopiasAutomaticas`: cada frecuencia, cambio de día, de mes y de año, mes corto, semanal el mismo día antes y
  después de la hora, retraso de varios períodos (una sola copia), reintento a los 30 minutos, y validación de rangos.
- Integración contra PostgreSQL real, con reloj simulado: hace la copia cuando toca y no antes, no repite, retención (borra solo lo
  suyo y deja otros archivos), archivo temporal viejo, falla (carpeta de solo lectura) con mensaje y reintento, «ahora», lista y
  descarga con nombres maliciosos (`..\`, `../`, nombre ajeno), permisos 401 y 403, y que una copia automática pase la comparación y
  la verificación de firma como las demás.
- Frontend: texto de la próxima copia, validación del formulario.
- A mano, en un Docker aparte: activar, «Hacer una copia ahora», ver el archivo en `backups/automaticas`, descargarlo, cargarlo en
  «Seleccionar copia» y confirmar que se compara como idéntico.

---

## Documentación

`docs/BACKUPS.md` (la sección «Hacerla automática» pasa a tener dos caminos: desde el sistema y con el Programador de tareas),
`docs/SEGURIDAD.md`, `docs/POLITICA_DE_SEGURIDAD.md` si corresponde, `README.md`, `docs/ARQUITECTURA.md`, `docs/BASE_DE_DATOS.md`
(dos tablas nuevas y el cambio de `TurnosEncargado`), el manual dentro del sistema (secciones Calendario y Copia de seguridad) y el zip
del proyecto.

## Riesgos

- **Plazo:** la defensa es el 12/11. Dos migraciones nuevas son el riesgo principal; por eso se prueba cada una con datos viejos y se
  deja de agregar funciones después de esto.
- **Docker:** el cambio de `docker-compose.yml` no se aplica solo; hay que recrear el contenedor. Tras compilar en Windows, reiniciar el
  backend (`dotnet watch`).
- **Video de demostración:** los capítulos de Calendario y de Copia de seguridad quedan desactualizados; se pueden volver a grabar.

## Criterios de aceptación

1. Se cargan entrenadores desde el sistema (Administrador y Contador) y Recepción los ve.
2. El calendario solo acepta entrenadores activos, uno por hora; el PDF sale con sus nombres.
3. Con turnos ya cargados, la migración conserva el calendario tal cual, y se puede deshacer.
4. En *Copia de seguridad* se programa cada 6 h, cada 12 h, diaria, semanal o mensual, y se ve cuándo es la próxima.
5. «Hacer una copia ahora» deja un `.zip` firmado en `backups/automaticas` y aparece en la lista.
6. Una copia que toca se hace sola, sin repetirse; si el sistema estuvo apagado, se hace al volver; y se conservan solo las últimas N.
7. Un fallo se ve en pantalla y no tumba el sistema; no hay forma de descargar nada fuera de la carpeta.
8. Toda la suite de pruebas pasa y la documentación y el zip reflejan los cambios.
