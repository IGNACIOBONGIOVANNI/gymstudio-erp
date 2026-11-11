# GymStudio ERP — Sistema contable

Sistema de gestión para un gimnasio: contabilidad por partida doble (Libro Diario,
Mayor, Balance), facturación, caja, banco, stock, socios, planes y membresías.

- **Backend:** .NET 10 Web API · Clean Architecture · MediatR · EF Core · PostgreSQL
- **Frontend:** React 19 · TypeScript · Vite · Tailwind · TanStack Query/Table
- **Autenticación:** JWT + contraseñas hasheadas (PBKDF2, `PasswordHasher` de ASP.NET Identity)

## Cómo levantarlo

### Con Docker (recomendado, es como se instala en otra PC)

Requisitos: Docker Desktop abierto y conexión a internet **la primera vez**
(descarga imágenes y dependencias; tarda entre 5 y 10 minutos).

1. Copiar el proyecto a la PC: descomprimir `GymStudio-ERP.zip` (o `git clone`).
   No copiar la carpeta de desarrollo tal cual: trae `bin/`, `obj/` y
   `node_modules/` de Windows, que no sirven dentro de los contenedores.
2. En la carpeta del proyecto:

   ```bash
   docker compose up --build
   ```

3. Esperar a que el backend muestre `Now listening on: http://0.0.0.0:5122` y abrir
   http://localhost:5173. Ingresar con alguno de los usuarios iniciales (ver abajo).

- API + Swagger: http://localhost:5122/swagger
- PostgreSQL (para pgAdmin o psql): `localhost:5433`, usuario `postgres`, contraseña `password`

Los tres puertos (5173, 5122 y 5433) solo escuchan en esta PC (`127.0.0.1`): otra computadora de la red no puede
conectarse al sistema ni a la base. Para abrirlo a la red a propósito, sacar `127.0.0.1:` de `docker-compose.yml`.

### Copias de seguridad

Los datos viven en el volumen de Docker: se pierden con `docker compose down -v` o con la PC.

**Desde el sistema** (solo Administrador): *Configuración › Copia de seguridad › Copia completa del sistema*.
**Descargar copia completa (.zip)** guarda **todas las tablas** (facturas, asientos, caja, banco, stock, socios, usuarios y
auditoría) con un manifiesto y una huella de cada una. **Seleccionar copia (.zip)** muestra una vista previa que la compara con el
sistema actual, tabla por tabla, y desde ahí se puede restaurar: pide escribir `RESTAURAR`, descarga antes una copia de lo que hay y
es todo o nada (si algo falla, no queda nada a medias). Descargar y restaurar quedan en la Auditoría.

**Copia automática** (en la misma pestaña, solo Administrador): se programa **cada 6 h, cada 12 h, todos los días, una vez por semana o
una vez por mes**, a la hora de Argentina que se elija, y el sistema hace solo la misma copia completa y firmada, la guarda en la carpeta
`backupsutomaticas` del proyecto y conserva las últimas N (de 1 a 60). La pantalla muestra la próxima y la última copia (y el motivo si
una falló, con reintento a los 30 minutos), tiene el botón **Hacer una copia ahora** y la lista de las guardadas para descargarlas. Si
Docker estaba apagado a la hora programada, hace una sola copia al arrancar. Para que la carpeta quede en la PC y no se pierda al recrear
el contenedor, el `docker-compose.yml` monta `./backups`: tras actualizar el proyecto hay que recrear el backend una vez
(`docker compose up -d`).

**Con scripts** (en PowerShell, con el sistema levantado; es lo que usa la copia diaria programada):

```powershell
.\scripts\backup.ps1                # copia en la carpeta backups\ (verificada, con huella SHA-256 y manifiesto)
.\scripts\programar-backup.ps1      # la hace sola todos los días
.\scripts\comparar-backup.ps1 -Archivo .\backups\gymstudio_AAAAMMDD_HHMMSS.dump   # ¿coincide con el sistema actual?
.\scripts\restaurar.ps1 -Archivo .\backups\gymstudio_AAAAMMDD_HHMMSS.dump -Confirmar
```

Cada copia trae junto al archivo `.dump` su huella SHA-256 y un **manifiesto** (`.manifiesto.json`) con la versión del
sistema, la migración de la base y una huella de los datos de cada tabla. `comparar-backup.ps1` no modifica nada: verifica
que el archivo no esté dañado, compara la versión y el esquema con los de ahora y dice, tabla por tabla, qué cambió desde
la copia (o que es **idéntica** al sistema actual). `restaurar.ps1` hace esa misma comparación antes de reemplazar nada y se
niega a restaurar una copia de una versión más nueva que el código instalado (salvo `-Forzar`).

Detalles y recomendaciones en [`docs/BACKUPS.md`](docs/BACKUPS.md).

Para empezar de nuevo con la base vacía (**borra todos los datos cargados**):

```bash
docker compose down -v
```

Si al levantar aparece *port is already allocated*, hay otro programa usando el
5173, el 5122 o el 5433 (por ejemplo un `npm run dev` o un backend abierto): cerrarlo
y volver a ejecutar `docker compose up`.

### Local (sin Docker para backend y frontend)

Requiere PostgreSQL en `localhost:5432` (ver `ConnectionStrings` en
`backend/src/EstudioContable.API/appsettings.json`).

```bash
dotnet run --project backend/src/EstudioContable.API --urls http://localhost:5122
```

```bash
cd frontend && npm install && npm run dev
```

Al arrancar, el backend aplica las migraciones pendientes y, si la tabla de
usuarios está vacía, crea los usuarios iniciales.

## Usuarios iniciales

| Usuario (email)            | Contraseña | Rol           |
|----------------------------|------------|---------------|
| `jefe@gym.com`             | `123456`   | Administrador (principal) |
| `contador@gymstudio.com`   | `123456`   | Contador      |
| `recepcion@gymstudio.com`  | `123456`   | Recepción     |

La contraseña inicial sale de `UsuariosIniciales:Password` en
`appsettings.Development.json`. Sólo se usa la primera vez (tabla vacía): después
se cambian desde **Admin de Usuarios**, y el sistema nunca las pisa.

## Manual de usuario y Acerca de

Al pie del menú lateral hay dos accesos que ven todos los usuarios:

- **Manual:** abre la guía de uso en **otra ventana** (`/manual`, o http://localhost:5173/manual). Explica pantalla por
  pantalla cómo cargar socios, cobrar cuotas, facturar ventas y compras, usar Caja y Banco, leer los libros y administrar
  usuarios, con un índice que acompaña la lectura, un glosario y una tabla de «Si algo no sale». No pide iniciar sesión, se
  adapta al celular y se imprime en blanco y negro (botón *Imprimir o guardar en PDF*). Se puede abrir en una sección
  puntual, por ejemplo `/manual#facturar`.
- **Acerca de:** diálogo con la versión, lo que incluye el sistema, la tecnología, las materias del proyecto y quién tiene
  la sesión abierta. La versión, los autores y la institución que se muestran se cambian en `frontend/src/acercaDe.ts`.

El texto del manual está en `frontend/src/features/manual/secciones.tsx` (y su índice en `indice.ts`): si se agrega o se
cambia una pantalla, se actualiza ahí.

## Roles y permisos

Toda la API exige iniciar sesión, salvo `POST /api/auth/login`. Los permisos se
controlan en el backend; el frontend sólo oculta menús.

| Acción                                                    | Administrador | Contador | Recepción |
|-----------------------------------------------------------|:---:|:---:|:---:|
| Consultar libros, facturar, socios, planes, stock          | ✔ | ✔ | ✔ |
| Ver los importes del resumen de inicio (ventas, caja, IVA) | ✔ | ✔ | ✘ |
| Anular o editar facturas                                   | ✔ | ✔ | ✘ |
| Ver cuentas a cobrar y a pagar y estados de cuenta         | ✔ | ✔ | ✘ |
| Asignar, cambiar y finalizar la rutina de un socio         | ✔ | ✔ | ✔ |
| Crear, editar y eliminar rutinas modelo y ejercicios       | ✔ | ✔ | ✘ |
| Registrar y corregir mediciones de peso y medidas          | ✔ | ✔ | ✔ |
| Eliminar mediciones                                        | ✔ | ✔ | ✘ |
| Registrar ingresos de socios y ver los socios en riesgo    | ✔ | ✔ | ✔ |
| Deshacer un ingreso cargado por error                      | ✔ | ✔ | ✘ |
| Crear, confirmar o anular asientos manuales; crear cuentas | ✔ | ✔ | ✘ |
| Administrar usuarios y consultar la auditoría              | ✔ | ✘ | ✘ |
| Descargar, comparar, restaurar y programar copias de seguridad | ✔ | ✘ | ✘ |

El administrador principal no puede eliminarse, desactivarse ni perder el rol.

## Seguridad

Resumen de lo que protege el sistema. El detalle, cómo se comprobó y los problemas que se
encontraron y corrigieron están en [`docs/SEGURIDAD.md`](docs/SEGURIDAD.md).

- **Sesión:** JWT firmado que vence a las 8 horas (`Jwt:ExpiracionMinutos`). En cada pedido se verifica en la base que el
  usuario siga activo y con el mismo rol: una baja o un cambio de rol rigen al instante.
- **Contraseñas:** guardadas con hash PBKDF2; las nuevas exigen mínimo 8 caracteres, una mayúscula, un número, un carácter especial y que no haya números consecutivos como 12 o 98
  (lo valida el servidor, y el formulario de usuarios muestra en vivo qué falta). Los usuarios de ejemplo del README (clave `123456`) no la cumplen: son
  solo para la demostración y hay que cambiarlos antes de usar datos reales.
- **Fuerza bruta:** límite de intentos de login por IP (`Auth:IntentosLoginPorMinuto`, 10 por defecto) y cuenta bloqueada
  15 minutos tras 5 claves incorrectas. El mensaje es el mismo para un email inexistente y para una clave errónea, y también
  tarda lo mismo (no se puede descubrir qué emails existen midiendo el tiempo). Los emails y claves absurdos (muy largos o con
  caracteres de control) se rechazan sin consultar la base ni gastar CPU.
- **Permisos:** los decide el servidor (tabla de arriba); ocultar un menú en pantalla no es una medida de seguridad.
- **Navegador:** CORS restringido a `Cors:OrigenesPermitidos` y cabeceras de seguridad en la API
  (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Content-Security-Policy`, `Cache-Control: no-store`).
- **Datos:** consultas parametrizadas, partida doble e inmutabilidad de asientos controladas por la propia base de datos,
  y registro de auditoría de quién cambió qué (solo lectura desde la API).
- **Validaciones:** los números, códigos, tipos y documentos tienen forma y tope en el dominio (por ejemplo, un plan dura de 1 a
  120 meses, un CUIT tiene que ser válido y un código de cuenta son números separados por puntos), así que valen por cualquier
  camino. El texto libre se acepta tal cual, sin caracteres de control, porque se guarda con parámetros y se muestra escapado.
- **Archivos:** la importación de Excel acepta solo `.xlsx` de hasta 5 MB, corta las bombas zip descomprimiendo hasta un tope,
  limita las filas y omite (contándolas) las que no tienen forma de factura.
  En *Configuración > Copia de seguridad*, **Cargar configuración** no aplica el archivo directo: muestra primero una vista previa
  que compara la configuración actual con la del archivo dato por dato (el valor de ahora, el del archivo y si cambia, queda igual o no
  se carga; hay un interruptor para ver solo lo que cambia), y solo carga, al confirmar,
  los datos conocidos y con forma válida; lo que no es de la configuración se ignora y lo inválido se informa y no se carga.
- **Auditoría del equipo:** cada cambio registra desde qué **IP**, **sistema operativo** y **navegador** se hizo, y cada inicio
  de sesión (correcto, fallido o sobre una cuenta bloqueada) también queda registrado, sin guardar nunca la contraseña. La IP solo
  se toma de `X-Forwarded-For` si el pedido llega de una red de confianza (`Proxy:RedesDeConfianza`, por defecto las redes
  privadas), así nadie puede falsearla con el encabezado. Sistema operativo y navegador salen del `User-Agent`, que el propio
  equipo informa: sirve para saber desde dónde se trabajó, no como prueba.
- **Red:** los puertos de Docker solo escuchan en `127.0.0.1` (ver arriba) y los errores internos no muestran trazas.
- **Copias de respaldo:** la copia completa del sistema se descarga, se compara y se restaura desde la pantalla (solo Administrador),
  verificada con huellas SHA-256 por archivo y por tabla; la restauración es todo o nada, con confirmación escrita, y la copia
  no puede apuntar a otras tablas ni inflarse (tope de 50 MB / 300 MB descomprimido). Ver *Copias de seguridad*.
- **Política de seguridad:** [`docs/POLITICA_DE_SEGURIDAD.md`](docs/POLITICA_DE_SEGURIDAD.md) fija objetivo, alcance, roles, reglas
  (cada una dice si la hace cumplir el sistema o dependen de las personas), gestión de incidentes y limitaciones conocidas. También
  está en el manual y se abre desde *Configuración › Seguridad y Sistema › Ver política de seguridad*.

Antes de usarlo con datos reales hay que cambiar las contraseñas iniciales y la clave de PostgreSQL, correr en modo
`Production` (así Swagger no queda publicado) y servirlo por HTTPS con un proxy; ver *Configuración fuera de desarrollo*
y la sección *Pendiente* de `docs/SEGURIDAD.md`.

## Precio de los productos al facturar

Al elegir un producto en **Facturar**, el sistema propone su **precio de venta** si la operación es una venta y su
**precio de compra** (costo) si es una compra. Si después se cambia entre Venta y Compra, los productos ya elegidos
pasan al precio de la nueva operación; un precio escrito a mano no se pisa. Debajo de cada producto se aclara de dónde
salió el precio. Un producto que nació de una compra no tiene precio de venta hasta su primera venta: ahí se escribe
a mano y queda guardado.

## Numeración de comprobantes

- **Ventas:** el sistema las numera solo, correlativas por tipo de comprobante y punto de venta (ej. `0001-00000007`).
- **Compras:** el número de la factura del proveedor es **opcional**. Si se deja vacío, el sistema asigna un número
  interno consecutivo con el punto de venta reservado `9999` (ej. `9999-00000001`), pensado para ejercicios y
  demostraciones. Si se escribe el número real, no se puede cargar dos veces la misma factura del mismo proveedor.

## Anulación de facturas (trazabilidad contable)

Las facturas y los asientos confirmados **no se borran**:

- **Anular** una factura la marca como anulada y deja su asiento en estado
  **Anulado** (sigue en la base, pero no suma en el Diario, el Mayor ni el
  Balance), con el movimiento inverso de caja/banco y la devolución de stock.
  Así el IVA Crédito Fiscal nunca tiene Haber ni el Débito Fiscal tiene Debe.
- **Editar** una factura revierte los efectos de la versión anterior y aplica los
  de la nueva.
- En el Libro Diario, "anular" un asiento borrador lo elimina; uno confirmado
  queda Anulado (o anula la factura a la que pertenece).

## Control de integridad, vista previa del asiento y novedades

- **Control de integridad** (menú *Contabilidad*, solo Administrador y Contador): el sistema se audita a sí mismo
  en vivo con 16 controles de solo lectura. Entre otros: Debe = Haber en todo el Diario y en cada asiento,
  Activo = Pasivo + Patrimonio + Resultado, Libro Caja y Banco = sus cuentas del Mayor, neto e IVA de las facturas =
  Ventas, Compras e IVA del Mayor, cuentas a cobrar y a pagar = Deudores por Ventas y Proveedores, toda factura con
  su asiento, anuladas sin efecto, sin stock negativo y sin números de venta repetidos. Si alguien toca un dato por
  fuera de las reglas (por ejemplo, un movimiento insertado directo en la base), el control correspondiente da la
  diferencia exacta.
- **Vista previa del asiento** (pantalla *Facturar*): antes de emitir, muestra qué cuentas se debitan y se
  acreditan. Usa la misma función que arma el asiento real, así que lo que se ve es lo que se registra.
- **Novedades** (pantalla de inicio): frases automáticas calculadas con los propios datos, sin servicios externos
  (ventas contra el mes anterior, deudas de más de 90 días con el nombre del cliente, productos sin stock,
  cuotas por vencer...). Primero lo que requiere atención. Recepción solo ve las del gimnasio.
- **Campanita de avisos** (arriba a la derecha, en todas las pantallas): muestra las mismas novedades y un número con
  las que requieren atención. Al entrar avisa una vez cuántas hay y, mientras se trabaja, solo muestra un cartel
  cuando aparece un aviso nuevo. Se actualiza sola cada minuto y al cambiar de pantalla. No usa internet ni
  credenciales, así que funciona igual dentro de Docker.

## Asistente

El botón **Asistente** (abajo a la derecha, en todas las pantallas) contesta preguntas escritas con tus palabras, con
los datos del sistema. Funciona **por reglas**: reconoce palabras clave y períodos, consulta la base y arma la
respuesta; no usa internet ni inteligencia artificial externa, así que anda igual sin conexión. Es de solo lectura.

- Entiende, por ejemplo: "¿cuánto vendí este mes?", "ventas de marzo de 2026", "¿cuánto gasté el mes pasado?",
  "resultado de este año", "¿cuánto IVA tengo que pagar?", "¿cuánto hay en caja?", "¿quién me debe?", "¿a quién le
  debo?", "¿cuál es el producto más vendido?", "¿cuánto stock hay de proteína?", "¿qué productos están sin stock?",
  "¿cuántas cuotas vencen esta semana?", "¿quiénes no tienen rutina?", "¿está todo bien con la contabilidad?" y
  "dame un resumen". Los períodos: hoy, ayer, esta semana, la semana pasada, este mes, el mes pasado, un mes con
  nombre (y año) y este año.
- **Gimnasio** (todos los perfiles): "¿quién vino hoy?", "¿cuántas visitas hubo este mes?", "¿quiénes no vienen hace
  tiempo?" (o "hace 30 días", "hace 2 semanas"), "¿cuántos socios nuevos hay este mes?", "¿qué planes hay?" y la
  **ficha de un socio** por apellido, nombre o DNI: "¿cuándo vence la cuota de Gimnez?" o "ficha del DNI 23232323"
  (cuota, plan, rutina, última visita y visitas del mes). Si hay dos socios con el mismo apellido, pide el DNI.
- **Comparaciones** (solo Administrador y Contador): "¿vendí más que el mes pasado?", "¿gasté menos que ayer?",
  "comparame el resultado de este mes con el anterior" (diferencia en pesos y en %).
- **Un cliente o proveedor puntual:** "¿cuánto me debe Ferretería Sur?", "¿cuánto le debo a Distribuidora Norte?".
- Ignora tildes, mayúsculas y signos. Si no entiende, lo dice y sugiere preguntas.
- **Permisos en el servidor:** lo financiero (ventas, caja, deudas, IVA, contabilidad) solo se le contesta a
  Administrador y Contador; Recepción recibe ayuda con socios, cuotas, rutinas, ingresos, planes y stock.

## Cuentas a cobrar y a pagar

Menú **Cuentas a cobrar y pagar** (solo Administrador y Contador). Para cada cliente (o proveedor) muestra
lo facturado en **cuenta corriente**, lo cobrado (o pagado), el saldo y la antigüedad de la deuda (0 a 30,
31 a 60, 61 a 90 y más de 90 días), con su estado de cuenta en pantalla y en PDF.

- Un **cobro a un cliente** se carga en Caja o Banco como ingreso con la contrapartida *Cobro a clientes
  (Deudores por Ventas)* y **eligiendo el cliente**; un **pago a un proveedor**, como egreso con *Pago a
  proveedores* y eligiendo el proveedor. Sin elegirlo el sistema no deja guardar.
- Los pagos no se asignan a una factura puntual: se aplican a las más antiguas primero, y lo que sobra
  queda como saldo a favor.
- Las facturas anuladas no cuentan. Eliminar o corregir un cobro actualiza la deuda.
- **Control contable:** el total debe ser igual al saldo de *Deudores por Ventas* (o *Proveedores*) del
  Libro Mayor; si no, la pantalla avisa y explica la causa probable (cobros viejos sin cliente asignado o
  asientos manuales). El resumen de inicio muestra lo mismo como "A cobrar" y "A pagar".

## Caja y Banco

- Cada movimiento cargado en el Libro Caja o Banco genera su asiento contra la
  cuenta elegida (Otros Ingresos, Gastos Generales, Deudores por Ventas o
  Proveedores). Editar o eliminar anula el asiento anterior.
- **Transferir a Banco / Transferir a Caja**: egreso en un libro, ingreso en el
  otro y un único asiento. No se edita; si hay un error se elimina (se borran los
  dos movimientos) y se carga de nuevo.
- Caja y Banco no se usan en asientos manuales: son cuentas de control cuyo saldo
  sale sólo de sus libros y de las facturas, así el Libro Caja siempre coincide con
  el Mayor.

## Resumen de inicio (dashboard)

La primera pantalla muestra, para el mes elegido: ventas y compras netas, resultado, IVA a pagar
(débito menos crédito fiscal), saldos de Caja y Banco, facturas anuladas, ventas vs. compras de los
últimos 6 meses, ingresos por origen (cuotas o productos), productos más vendidos, estado de las
cuotas de los socios, socios sin rutina, rutinas vencidas y productos con poco stock (5 unidades o
menos). El botón **Imprimir resumen** lo saca en PDF.

- Los totales salen de las mismas tablas que los libros, así que coinciden con el Diario, la Caja y
  el Banco; las facturas anuladas no suman.
- Los importes los calcula y los entrega el servidor solo a Administrador y Contador; Recepción
  recibe únicamente lo del gimnasio.

## Rutinas de gimnasio

Menú **Rutinas** (y el botón de la pesa en **Socios**):

- **Ejercicios**: catálogo con grupo muscular. Si un ejercicio está en alguna rutina no se
  borra: queda desactivado.
- **Rutinas modelo**: de 1 a 7 días, cada uno con sus ejercicios, series, repeticiones, descanso
  e indicaciones. Se cargan dos de ejemplo la primera vez.
- **Rutina de un socio**: se asigna con fecha de inicio y vencimiento opcional. Un socio tiene
  una sola rutina activa (la base lo garantiza con un índice parcial); al asignar otra, la
  anterior se cierra el día que empieza la nueva. Queda el historial.
- **Imprimir PDF** de la rutina del socio, en blanco y negro, con una columna para anotar el peso.

## Entrenadores y calendario

Dos ítems del menú, en el grupo Recepción: **Entrenadores** (las fichas) y **Calendario** (quién está a cargo de cada hora).

### Entrenadores

- Cada ficha tiene **foto**, nombre, teléfono, **WhatsApp**, **email**, especialidad, las horas por semana que tiene en el calendario
  y su estado. Son fichas, no usuarios: no tienen contraseña ni entran al sistema. El nombre no se repite aunque cambien las
  mayúsculas o los espacios. Un entrenador no se borra: se **desactiva**, y solo si no tiene horas en el calendario (el sistema avisa
  cuántas son).
- Los ven todos los usuarios; los cargan, editan y desactivan, y les ponen o sacan la foto, el **Administrador y el Contador** (el
  servidor lo exige: Recepción recibe 403). Recepción ve la lista y puede escribirles: el WhatsApp es un botón que abre el chat en otra
  pestaña y el email abre el programa de correo.
- **La foto** se elige desde la computadora o el celular: el navegador la recorta al centro y la achica a 256 por 256 antes de subirla, y
  el servidor vuelve a comprobar que sea un JPEG de verdad (100 a 150 000 bytes, de 16 a 512 píxeles de lado). Sin foto, o si no se puede
  cargar, se ven las iniciales. Vive en su propia tabla, `FotosDeEntrenador`, sin auditoría de filas (cada subida y baja queda como un
  evento) y entra en las copias de seguridad.
- El teléfono y el WhatsApp se cargan con el país y el número (ver «Nombres y teléfonos de las personas»); el nombre lleva solo letras.

### Nombres y teléfonos de las personas

- **El nombre** de un entrenador, un socio (nombre y apellido) o un usuario lleva solo letras (con tildes, ñ y ü), espacios, puntos
  (Lic.), apóstrofos (D'Angelo) y guiones (Pérez-Gómez), y empieza con una letra. En pantalla lo que no corresponde no se escribe; el
  servidor lo vuelve a exigir (400 con el motivo). Los nombres de empresa (clientes, proveedores, razón social) siguen siendo libres.
- **El teléfono y el WhatsApp** se eligen por país (bandera, nombre y código, con buscador; Argentina viene elegida) y se escribe el
  número solo con dígitos. Se guarda como `+54 1155551234`: `+`, código del país (1 a 3 dígitos), un espacio y el número (4 a 14
  dígitos; entre código y número hasta 15, el máximo internacional). Pegar `+54 9 11 5555-1234` elige el país solo. El servidor exige
  esa forma exacta en las tres pantallas.
- Las banderas son imágenes SVG del paquete `country-flag-icons` (licencia MIT) que viajan con la aplicación: se ven igual en
  Windows (donde los emoji de bandera aparecen como letras) y no necesitan internet.
- **Datos cargados antes** (teléfonos sin país, nombres con números): no se tocan en la base. El formulario lee un teléfono viejo como
  argentino y lo deja en el formato nuevo al guardar; si trae texto o varios números, queda vacío en vez de inventar uno. Un nombre
  viejo con números hay que corregirlo para poder guardar la ficha.

### Horas cumplidas y presupuesto de pago

- Solo para el **Administrador y el Contador** (el servidor lo exige: Recepción recibe 403 y ni siquiera recibe el valor de la hora). Son dos
  pestañas más de **Entrenadores**, al lado de la **Lista**.
- En la ficha, el **valor de la hora** de cada entrenador (vacío = sin valor: todavía no se le pueden cargar horas).
- **Horas**: por mes, cada día muestra las horas que le tocaban según el calendario («Le tocaba») y las que cumplió («Cumplió», de a cuarto
  de hora; 0 = faltó; vacío = sin cargar). **Precargar desde el calendario** carga de una vez, hasta hoy, lo que le tocaba a cada entrenador activo con
  valor de la hora, sin pisar lo ya cargado y repetible; después se corrige lo que haga falta y se pueden **agregar días** que el calendario no
  tenía. Cada día guarda el valor de la hora que tenía al cargarse: si el valor sube después, los días ya cargados no cambian.
- **Presupuesto**: por período (hasta 62 días), las horas, el valor de la hora y el monto de cada entrenador y el total, con **Descargar PDF**
  (A4 en blanco y negro). El monto de un día es horas por valor, redondeado al centavo; el total es la suma de los días.
- Se guarda en `HorasDeEntrenador` (un registro por entrenador y día, con restricciones y auditoría) y entra en las copias de seguridad.

### Calendario

El calendario dice qué entrenador está a cargo del gimnasio, de **lunes a viernes** y de **06:00 a 22:00** (16 turnos de una hora por
día). Es una semana tipo que se repite: no tiene fechas. Hay una sola persona por hora y solo figuran entrenadores: quien hace la parte
contable o administrativa no aparece. Si todavía no hay entrenadores cargados, el aviso del calendario lleva a **Entrenadores**.

- Lo ven todos los usuarios; lo modifican **Administrador y Contador** (el servidor lo exige: Recepción recibe 403).
- **Editar calendario**: se escribe el nombre del entrenador en cada hora (aparece la lista de los cargados; Enter y las flechas pasan
  a la hora de abajo o de arriba) o se usa **Asignar turno** para cargar a un entrenador en varios días y horas de una vez (por ejemplo,
  Ana de lunes a viernes de 06:00 a 14:00). Un nombre que no es de un entrenador activo se marca en rojo y no deja guardar. Los cambios
  se aplican con **Guardar cambios**; **Cancelar** pide confirmación si hay algo sin guardar.
- El mismo entrenador en horas seguidas se ve como un solo bloque, con un color suave por persona, y se muestran las horas semanales de cada uno.
- **Imprimir (PDF)**: hoja A4 apaisada en blanco y negro, con el logo y los datos del negocio, lista para fotocopiar. Las celdas
  libres quedan en blanco para anotar a mano.
- Se guarda en la base: la tabla `Entrenadores` (una fila por ficha, nombre único sin distinguir mayúsculas) y `TurnosEncargado`
  (una fila por celda cargada, con clave foránea al entrenador que no permite borrar a uno con turnos), con restricciones que repiten
  las reglas (días 1 a 5, horas 6 a 21, un entrenador por celda) y con auditoría de quién lo cambió. El servidor guarda solo las
  diferencias. Entran en las copias de seguridad como cualquier otra tabla.

## Ingreso de socios y riesgo de abandono

Pantalla **Recepción**, arriba de todo:

- **Ingreso por DNI**: se escribe el DNI (con o sin puntos) y el sistema registra la visita y muestra, en grande,
  el nombre, cómo está la cuota (al día, por vencer, vencida o sin plan, con ícono y texto) y **qué día de la rutina
  le toca hoy** con sus ejercicios. Los días se rotan en orden, uno por cada visita desde que empezó la rutina.
  Si la cuota no está al día aparece el botón para cobrarla ahí mismo.
- **Una visita por socio y día**: si pasa dos veces, el sistema muestra lo mismo y no duplica (lo garantiza un índice
  único en la base, también ante dos recepciones cargando a la vez). Un ingreso cargado por error lo deshace
  Administrador o Contador.
- **Ingresos de hoy**: quién entró, a qué hora, cómo estaba su cuota y quién lo registró.
- **Socios en riesgo de abandono**: los que tienen la cuota al día pero hace 14 días o más (se puede cambiar a 7, 21
  o 30) que no vienen. Cada uno tiene un botón que abre WhatsApp con el mensaje ya escrito. Los mismos aparecen en las
  novedades del resumen y en la campanita. Un socio que nunca vino cuenta desde el día que se anotó.
- Un socio con ingresos registrados no se puede eliminar, para conservar el historial.
- No usa internet ni credenciales: funciona igual dentro de Docker.

## Seguimiento de socios, avisos y exportación

- **Seguimiento** (botón de la curva en **Socios**): peso y, si se midieron, grasa corporal y
  perímetros (cintura, cadera, pecho, brazo, muslo). Una medición por socio y día; hay un gráfico de
  evolución, la variación desde la primera medición y PDF para imprimir.
- **Avisos por WhatsApp** (pantalla **Recepción**): filtros "Por vencer en 7 días" y "Vencidos" y un botón
  que abre WhatsApp con el mensaje de la cuota ya escrito. Usa el país del teléfono guardado; los cargados antes sin país
  se toman como argentinos y se entienden como se escriben allá (0, 15, +54 9). No usa ningún servicio externo, lo envía la
  persona desde su WhatsApp.
- **Exportar a Excel**: cada libro (Diario, Mayor, Balance, IVA Ventas y Compras, Caja, Banco, Stock) tiene,
  junto a "Imprimir PDF", un botón que descarga la planilla `.xlsx` con el mismo período y los mismos totales.

## Base de datos

Triggers (partida doble, inmutabilidad de asientos, auditoría), restricciones
`CHECK`, vista del Libro Mayor y función de sumas y saldos: ver
[docs/BASE_DE_DATOS.md](docs/BASE_DE_DATOS.md). El esquema completo está en
[01_esquema_base_datos.sql](01_esquema_base_datos.sql).

## Tests

```bash
dotnet test backend/EstudioContable.slnx
cd frontend && npm test
```

- **Dominio** (`backend/tests/EstudioContable.Domain.Tests`, sin base de datos):
  partida doble, asientos anulados, CUIT, DNI, renovación de membresías, stock,
  movimientos de caja y banco, usuarios, rutinas y su asignación a socios, y las
  reglas del ingreso (estado de la cuota, día de rutina que toca, riesgo de abandono).
- **Integración** (`backend/tests/EstudioContable.IntegrationTests`): levantan la
  API completa contra un PostgreSQL real y descartable (Testcontainers, requiere
  Docker abierto). Facturar, editar y anular dejando caja, Mayor y stock
  cuadrados; caja negativa; transferencias; permisos por rol; y los triggers y la
  auditoría probados con SQL directo.
- Cada test protege una regla concreta. Si se rompe la regla, el test falla (se
  verificó rompiendo el código a propósito).

En GitHub, el workflow `.github/workflows/ci.yml` corre los tests y el build del
frontend en cada push.

## Configuración fuera de desarrollo

La clave que firma las sesiones (JWT) no está en el repositorio. Si no se configura, cada instalación genera la
suya al azar la primera vez que arranca y la guarda en `backend/src/EstudioContable.API/.secretos/jwt.key`
(carpeta que git ignora): así una copia del código no alcanza para fabricar una sesión. Borrar ese archivo
genera otra clave y obliga a todos a iniciar sesión de nuevo. Si no se configura `Copias:ClaveDeFirma`, la misma clave
firma las copias de seguridad que se hacen desde la pantalla: cada instalación verifica solo las suyas (para pasar datos
de una PC a otra se usan `backup.ps1` y `restaurar.ps1`, o se configura la misma `Copias:ClaveDeFirma` en las dos).
Fuera del entorno `Development` conviene definirla
por variable de entorno (mínimo 32 caracteres) y no configurar `UsuariosIniciales:Password`.

```bash
Jwt__Key=<clave-secreta-larga>
```
