# Entrenadores: menú propio, foto y contacto, horas cumplidas y presupuesto de pago

Fecha: 8/11/2026 · Estado: diseño aprobado en el chat, pendiente de revisión de este documento · Defensa: 12/11/2026

Continúa lo hecho el 7 y 8/11 (`docs/superpowers/specs/2026-11-07-entrenadores-y-copias-automaticas-design.md`, ya en `main`). Son dos
partes que se construyen, prueban y fusionan por separado: primero la A (chica), después la B (grande). Cada una tiene su migración y su
recorrido real en un Docker aparte, y se fusiona con backup antes.

## Lo pedido

> «El botón entrenador [tiene que estar] aparte junto a la otra grilla de botones, porque estaría bueno que se pueda cargar los datos del
> entrenador, las horas que cumplió, para hacer más tarde un presupuesto de pago; además, a la hora de cargar el entrenador, que se pueda
> poner fotos o forma de contacto.»

Aclaraciones del usuario en el chat: las horas se cargan **por día, precargadas desde el calendario** y corregibles si faltó o hizo más o
menos; el presupuesto llega hasta **horas, valor de la hora y monto, con PDF**.

## Fuera de alcance

- Liquidaciones guardadas, marca de «pagado», recibos numerados.
- Fichada de entrada y salida.
- Historial de valores de la hora por entrenador (cada día guarda el que tenía cuando se cargó, y nada más).
- Asientos contables del pago, o que el pago mueva Caja o Banco.
- Más de una foto por entrenador; fotos de socios; recorte o edición de la foto en pantalla (se achica y se centra sola).
- Que el entrenador entre al sistema.

---

## Parte A · Menú propio, foto y contacto

### Menú y pantallas

- Ítem **Entrenadores** en el grupo *Recepción*, a continuación de *Calendario* (id de vista `entrenadores`). Lo ven los tres roles; no
  lleva restricción de rol en el menú.
- Módulo nuevo `ModuloEntrenadores` (`frontend/src/features/entrenadores/`). Recibe `PestanaEntrenadores` y `ModalEntrenador`, que hoy
  viven en `features/calendario/` (se mueven, no se copian). La pestaña **Entrenadores** desaparece del Calendario: el módulo
  Calendario queda sin pestañas, y su aviso «Todavía no hay entrenadores cargados» conserva el botón **Ir a Entrenadores**, que ahora
  navega al ítem nuevo (`irA('entrenadores')`, pasado como propiedad).
- Pestañas del módulo: **Lista** (los tres roles). La Parte B suma **Horas** y **Presupuesto** (solo Administrador y Contador).
- Manual de usuario: sección nueva `entrenadores` y recorte de la de `calendario`; filas de permisos actualizadas.

### La ficha

Campos (los que ya existen no cambian): `Email` (hasta 254 caracteres; vacío = `null`; con forma `algo@dominio.ext`, sin espacios),
`WhatsApp` (hasta 30 caracteres; solo dígitos, espacios, `+`, `-`, `(` y `)`; vacío = `null`), además de teléfono y especialidad.
Un entrenador tiene como mucho **una foto de perfil**, que se puede cambiar o quitar.

En la lista: foto (o las iniciales en un círculo si no tiene), nombre, teléfono, **WhatsApp** (botón que abre el chat con el armado de
números argentinos que ya usa Recepción, `utils/whatsapp.ts`; si el número no se puede interpretar, se muestra el texto sin botón),
**email** (enlace `mailto:`), especialidad, horas por semana del calendario y estado. Recepción ve y llama; cargan y editan el
Administrador y el Contador.

### Datos

- `contabilidad."Entrenadores"`: columnas nuevas `Email` `varchar(254)` y `WhatsApp` `varchar(30)`, ambas nulas, con `CK_Entrenadores_Email`
  (`"Email" IS NULL OR "Email" ~ '^[^@\s]+@[^@\s]+\.[^@\s]+$'`).
- **Tabla aparte** `contabilidad."FotosDeEntrenador"`: `EntrenadorId` `bigint` (clave primaria y clave foránea a `Entrenadores`, `ON DELETE CASCADE`),
  `Foto` `bytea` no nulo con `CK_FotosDeEntrenador_Tamano` (`octet_length("Foto") BETWEEN 100 AND 150000`), `Actualizada` `timestamptz`.
  Va aparte para que la foto no se copie, en hexadecimal, a cada registro de la auditoría de `Entrenadores` (el disparador de auditoría
  guarda la fila entera), y para que la lista no la traiga. **Sin** disparador de auditoría: subir y quitar la foto se registran como
  evento (tabla `entrenador`, operaciones `FOTO` y `FOTO_BAJA`, de hasta 10 caracteres).
- Migración `FichaDeEntrenadorConContacto` (con `Down`). Etiquetas de la copia de seguridad: `contabilidad.FotosDeEntrenador` →
  «Fotos de los entrenadores» (grupo Gimnasio). Las copias de seguridad ya exportan con `COPY … CSV`, y el binario viaja en hexadecimal.

### API

- `GET/POST/PUT api/entrenadores` ya existen; `EntrenadorDto` suma `Email`, `WhatsApp`, `TieneFoto` y `FotoActualizada`; el cuerpo de alta y
  edición suma `Email` y `WhatsApp`. La lista no trae los bytes de la foto.
- `PUT api/entrenadores/{id}/foto` (`Roles.Gestion`): cuerpo crudo `image/jpeg`, tope de pedido de 160 000 bytes.
- `DELETE api/entrenadores/{id}/foto` (`Roles.Gestion`) → 204 aunque no tuviera.
- `GET api/entrenadores/{id}/foto` (cualquier usuario con sesión): `image/jpeg` con `X-Content-Type-Options: nosniff`,
  `Cache-Control: private, no-cache` y `ETag` derivado de `Actualizada`; 404 si no tiene foto.

### La foto, de punta a punta

- **Navegador:** al elegir el archivo (`accept="image/*"`), se lee, se recorta un cuadrado centrado y se achica a **256 × 256**, y se
  re-codifica a JPEG (calidad 0,85; si pasa de 150 000 bytes, baja la calidad hasta que entre). Un archivo que no es una imagen se rechaza
  con un mensaje junto al campo. La foto se muestra con `fetch` (lleva la sesión) y un `URL.createObjectURL`, no con `<img src>` directo.
- **Servidor (valida de nuevo, no confía en el navegador):** `ReglasDeFoto.Validar(byte[])` en el dominio. Tiene que ser un JPEG de verdad:
  empieza con `FF D8 FF`, termina con `FF D9`, se recorren sus segmentos hasta encontrar la cabecera de tamaño (`SOF0`–`SOF3`, `SOF5`–`SOF7`,
  `SOF9`–`SOF11`, `SOF13`–`SOF15`), y el ancho y el alto tienen que estar entre **16 y 512** píxeles. De 100 a 150 000 bytes. Cualquier otra
  cosa (PNG, SVG, HTML, un JPEG truncado, uno de 20 000 × 20 000) → 400 con un mensaje claro y no se guarda nada.

### Pruebas (A)

- Unitarias: `ReglasDeFoto` (JPEG válido mínimo armado a mano, firma errónea, sin fin, sin cabecera de tamaño, dimensiones fuera de rango,
  demasiado chico y demasiado grande), campos nuevos de `Entrenador` (email y WhatsApp válidos, inválidos y vacíos).
- Integración (PostgreSQL real): permisos de foto (Recepción 403 al subir y quitar, puede ver; anónimo 401), subir, reemplazar, ver y
  quitar; el ETag cambia al reemplazar; archivos hostiles rechazados (PNG con extensión falsa, JPEG truncado, 150 001 bytes, cuerpo vacío);
  la lista no trae bytes; email y WhatsApp inválidos → 400; que la foto entre en la copia de seguridad y vuelva igual al restaurar;
  restricciones de la base escribiendo directo; la migración con datos y su `Down`.
- Frontend: recorte cuadrado centrado (rectángulo de origen) para fotos apaisadas, verticales y cuadradas; reducción de calidad hasta el
  tope; enlace de WhatsApp y `mailto:`.

---

## Parte B · Horas cumplidas y presupuesto de pago

### Valor de la hora

`contabilidad."Entrenadores"."ValorHora"` `numeric(18,2)` nulo (nulo = todavía sin valor), `CK_Entrenadores_ValorHora` (`>= 0` y
`<= 999999999999`). Lo cargan y lo ven **solo** el Administrador y el Contador: para Recepción la API devuelve siempre `valorHora: null`
(el servidor lo oculta, no la pantalla). Cambiarlo queda en la auditoría de `Entrenadores`. Editar la ficha con `valorHora` ausente o nulo
lo borra.

### Datos de las horas

`contabilidad."HorasDeEntrenador"`:

| Campo | Regla |
|---|---|
| `Id` | clave |
| `EntrenadorId` | clave foránea a `Entrenadores`, `ON DELETE RESTRICT` (no se borra a quien tiene horas), con índice |
| `Fecha` | `date` |
| `Horas` | `numeric(4,2)`, de 0 a 24 y múltiplo de 0,25 (`CK_HorasDeEntrenador_Horas`); **0 = faltó** |
| `ValorHora` | `numeric(18,2)` no nulo, `>= 0`: **el valor de la hora de ese momento**, copiado al crear el día; no cambia si el del entrenador sube después |
| `Nota` | `varchar(200)` nula, sin caracteres de control |

Un solo registro por entrenador y día (`ux_horas_entrenador_fecha`). Migración `HorasYValorDeEntrenador` (con `Down`), disparador de
auditoría, etiqueta de la copia «Horas de los entrenadores» (grupo Gimnasio).

### Reglas (dominio, funciones puras en `ReglasDeHoras`)

- **Programadas:** las horas que el calendario le asigna a ese entrenador en ese día de la semana (lunes = 1 … viernes = 5; sábado y
  domingo, 0). Una fecha de fin de semana se puede cargar a mano.
- **Fecha:** no puede ser posterior a hoy (hora de Argentina, UTC-3, como el resto del sistema).
- **Monto de un día:** `Horas × ValorHora` redondeado a 2 decimales (`MidpointRounding.AwayFromZero`). **Monto de un entrenador en un
  período:** la suma de los montos de sus días. El total es la suma de los montos de los entrenadores. Todo con `decimal`.
- **Período:** `desde <= hasta`, de hasta **62 días**.

### API (todas `Roles.Gestion`: 401 sin sesión, 403 a Recepción)

- `GET api/entrenadores/horas?desde=aaaa-mm-dd&hasta=aaaa-mm-dd` → `{ entrenadores: [{ id, nombre, valorHora }], dias: [{ entrenadorId, fecha, programadas, cumplidas, valorHora, nota }] }`.
  Trae un renglón por cada día del período en que ese entrenador tiene horas programadas **o** ya tiene un registro; `cumplidas` es `null`
  si todavía no se cargó. Las entradas cargadas de un entrenador desactivado se siguen viendo.
- `PUT api/entrenadores/horas` con `{ items: [{ entrenadorId, fecha, horas, nota }] }`, hasta 500 renglones: para cada uno crea el día
  (con el valor de hora vigente del entrenador) o cambia sus horas y su nota (conservando el valor guardado); `horas: null` borra el registro.
  Todo o nada. Rechaza con 400: entrenador inexistente, fecha futura, horas fuera de rango o que no sean múltiplo de 0,25, nota inválida,
  un mismo entrenador y día repetido en el pedido, y crear un día para un entrenador **sin valor de hora** («Cargá el valor de la hora de
  X antes de cargarle horas.»). Cargar horas de un entrenador desactivado no se permite para días nuevos.
- `POST api/entrenadores/horas/precargar` con `{ desde, hasta }` → `{ creados, sinValor: [nombre] }`: para cada entrenador **con valor de hora** y cada
  día del período, hasta hoy, con horas programadas y sin registro, crea el día con `Horas = programadas`. No pisa lo ya cargado y
  se puede repetir sin efecto. Los entrenadores con horas programadas pero sin valor de hora se informan en `sinValor` y no se tocan.
- `GET api/entrenadores/presupuesto?desde&hasta` → `{ desde, hasta, filas: [{ entrenadorId, nombre, dias, horas, valorHora, monto }], totalHoras, totalMonto }`:
  solo los entrenadores con horas cargadas en el período; `dias` cuenta los días con `Horas > 0`; `valorHora` es el valor si fue el mismo todo
  el período y `null` si hubo más de uno (el monto siempre es la suma exacta de cada día). Orden alfabético.
- Cada `PUT` y cada `POST` queda en la auditoría de la tabla de horas.

### Pantallas

- **Horas:** selector de mes (por defecto el actual) y, por entrenador, una tabla con los días del período: fecha y día de la semana,
  «Le tocaba», «Cumplió» (campo numérico con paso de 0,25; vacío = sin cargar; 0 = faltó) y nota. Resumen por entrenador (horas cargadas
  y programadas). **Guardar cambios** envía solo lo modificado; **Precargar desde el calendario** llama a la precarga y avisa cuántos
  días creó y a qué entrenadores les falta el valor de la hora. Errores de validación junto a cada campo. Cambios sin guardar avisan
  antes de cambiar de mes o de pestaña.
- **Presupuesto:** `desde` y `hasta` (por defecto, del 1 del mes a hoy), tabla de entrenador, días, horas, valor de la hora, monto y fila de
  total, y botón **Descargar PDF**: A4 vertical, blanco y negro, con el logo y los datos del negocio (`pdfComun`), el período y la fecha
  de emisión. Importes en formato argentino (`$ 1.234,56`).
- En la **Lista**, Administrador y Contador ven además la columna y el campo **Valor de la hora**.

### Pruebas (B)

- Unitarias (`ReglasDeHorasTests`): horas programadas por día de la semana (incluido fin de semana), monto de un día con redondeo en el medio
  centavo, suma por entrenador y total, múltiplos de 0,25, período invertido y de más de 62 días, fecha futura.
- Integración (PostgreSQL real): permisos de las cuatro rutas (401, 403 a Recepción y a anónimo, y que `valorHora` llegue `null` a Recepción
  en la lista de entrenadores); precarga con calendario real (cantidad creada, que no pise lo cargado, idempotencia, entrenador sin valor
  informado y sin tocar, día sin horas programadas); el valor guardado **no cambia** al subir el valor del entrenador; el presupuesto con
  dos valores en el mismo período; borrar con `horas: null`; los rechazos de la lista de arriba sin cambiar nada; no se puede borrar a un
  entrenador con horas (restricción); restricciones de la base escribiendo directo; que las horas entren en la copia de seguridad; la migración
  con datos y su `Down`.
- Frontend: armado de los renglones del período, qué se envía al guardar (solo lo modificado), formato de importes y de horas, totales.
- A mano, en un Docker aparte: foto, WhatsApp, Horas, precarga, presupuesto y PDF, con los tres roles.

---

## Documentación

`docs/BASE_DE_DATOS.md` (tablas y columnas nuevas, restricciones, disparadores), `docs/SEGURIDAD.md` (foto: validación de JPEG, tope de tamaño,
`nosniff`; dinero visible solo para la gestión), `README.md`, el manual dentro del sistema (sección `entrenadores`) y el zip del proyecto.

## Riesgos

- **Plazo:** la defensa es el 12/11. Por eso va en dos entregas: la Parte A es chica y queda lista y fusionada primero; si la B se estira,
  `main` sigue entregable.
- **Foto subida por usuarios:** es la superficie nueva. Se valida en el navegador y otra vez en el servidor (formato real, tamaño en bytes y en
  píxeles), se sirve con tipo fijo y sin interpretación, y va en una tabla sin auditoría para no inflarla.
- **Dinero:** `decimal` de punta a punta, valor congelado por día, redondeo definido y probado, y datos de dinero ocultos para Recepción en el servidor.
- **Migraciones sobre la base real:** dos más. Cada entrega se fusiona con backup previo (`scripts\backup.ps1`), con el backend detenido mientras
  se fusiona; el Docker del usuario monta `./backend` con `dotnet watch`, que migra solo.

## Criterios de aceptación

1. Hay un ítem **Entrenadores** en el menú, visible para los tres roles, y el Calendario ya no tiene pestañas.
2. Se carga a un entrenador con foto, email y WhatsApp; la foto se ve en la lista; el botón de WhatsApp abre el chat; una foto que no es un
   JPEG válido se rechaza.
3. El Administrador y el Contador cargan el valor de la hora y las horas por día, precargadas desde el calendario y corregibles; Recepción
   no ve valores ni horas.
4. El valor de un día cargado no cambia si después sube el valor del entrenador.
5. El presupuesto de un período muestra horas, valor y monto por entrenador y el total, y se descarga en PDF en blanco y negro.
6. Toda la suite de pruebas pasa, la documentación refleja los cambios y las dos entregas se fusionan con backup previo.
