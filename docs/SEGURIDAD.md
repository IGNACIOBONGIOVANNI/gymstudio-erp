# Seguridad del sistema

Qué protege el sistema, cómo se comprobó y qué queda pendiente para un uso real.

## Qué hay implementado

| Capa | Medida |
|---|---|
| Autenticación | Inicio de sesión con JWT firmado (HS256, vence a las 8 h). Las contraseñas se guardan con el hash de ASP.NET Identity (PBKDF2). |
| Sesión | En cada pedido se comprueba en la base que el usuario siga activo y con el mismo rol que dice el token: una baja, una eliminación o un cambio de rol rigen al instante. |
| Fuerza bruta | Límite de intentos de login por IP y bloqueo de la cuenta 15 minutos tras 5 claves incorrectas. El mensaje es el mismo —y tarda lo mismo— para un email inexistente y para una clave incorrecta. Emails y claves absurdos se rechazan sin consultar la base. |
| Contraseñas | Mínimo 8 caracteres, una mayúscula, un número, un carácter especial y sin números consecutivos (12, 45, 98). Se exige en el servidor (`ReglasDeClave`), no solo en la pantalla, al crear un usuario o cambiarle la clave; el formulario muestra en vivo lo que falta. Las cuentas que ya existen entran con la clave que tienen. |
| Permisos | Toda la API exige sesión salvo el login. Tres roles: Administrador, Contador y Recepción. Recepción factura, cobra cuotas y gestiona socios; no mueve dinero (Caja, Banco, transferencias), no anula, no borra catálogos ni administra usuarios. |
| Navegador | CORS limitado a los orígenes del frontend. Cabeceras `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Cache-Control: no-store` y `Content-Security-Policy` en la API. |
| Datos | Consultas parametrizadas (EF Core). Restricciones en la base: partida doble, inmutabilidad de asientos, tabla de auditoría de solo lectura y auditoría automática de quién cambió qué. |
| Validaciones | Los números, códigos, tipos y documentos tienen forma y tope en el dominio (valen por cualquier camino). El texto libre no admite caracteres de control ni largos absurdos. |
| Archivos | Solo `.xlsx` de hasta 5 MB; se descomprime hasta un tope (bomba zip) y se limitan las filas. La foto de un entrenador: un JPEG de verdad de 100 a 150 000 bytes y de 16 a 512 píxeles de lado, validado en el servidor (ver la quinta revisión). |
| Auditoría del equipo | Cada cambio y cada inicio de sesión registran IP, sistema operativo y navegador. La IP solo se toma de `X-Forwarded-For` desde una red de confianza. |
| Copias | Copia completa descargable desde la pantalla (solo Administrador) con SHA-256 y manifiesto por tabla; vista previa que la compara con el sistema actual y restauración todo-o-nada con confirmación escrita. Se puede programar para que el sistema la haga sola (cada 6 h, 12 h, diaria, semanal o mensual) en `backups\automaticas`. Los scripts (`backup.ps1`, `comparar-backup.ps1`, `restaurar.ps1`) hacen lo mismo desde PowerShell. |
| Política | Reglas escritas de quién es responsable de qué, en [`POLITICA_DE_SEGURIDAD.md`](POLITICA_DE_SEGURIDAD.md) y en el manual (sección *Política de seguridad*). |
| Errores | Los errores internos no muestran trazas ni datos de la base. |
| Dependencias | Sin vulnerabilidades conocidas en NuGet ni en las dependencias de producción de npm. |

## Cómo se comprobó

Pruebas contra una copia local, con tokens falsificados, inyección, CORS, fuerza bruta y una matriz de permisos por rol. Los casos quedaron como tests automáticos (`TesoreriaYSeguridadTests`) y se ejecutan en cada push.

- Tokens: sin token, basura, firma cambiada, carga alterada, `alg: none` y firmado con otra clave. Todos dan 401.
- Inyección SQL en parámetros de consulta: sin errores 500 ni fuga de datos.
- Matriz de permisos de 25 rutas por rol.

## Problemas encontrados y corregidos

1. **Recepción podía mover dinero y borrar datos llamando a la API directamente.** La pantalla se lo ocultaba, pero el servidor no lo frenaba. Ahora el servidor responde 403.
2. **Un token seguía valiendo hasta 8 horas después de dar de baja al usuario.** Ahora deja de valer al instante.
3. Faltaban cabeceras de seguridad, el bloqueo por cuenta y una contraseña mínima razonable (bastaban 6 caracteres cualquiera).
4. Dependencias de npm con vulnerabilidades (DOMPurify, fast-uri, zod).

## Segunda revisión (rutinas, mediciones, resumen, cuentas corrientes y exportación)

Se repitió la auditoría sobre todo lo agregado después, contra una copia local: permisos de cada endpoint nuevo
por rol (Administrador, Contador, Recepción y sin sesión), datos mal formados y hostiles, asignación masiva,
inyección SQL, cabeceras, el Excel exportado, el código, las dependencias y la configuración de Docker.

Lo que ya estaba bien: todos los permisos coincidían con lo esperado; Recepción no recibe importes en el
resumen ni accede a cuentas corrientes; el cuerpo de un pedido no puede cambiar el id ni el socio de un registro;
el SQL siempre va parametrizado; el Excel guarda cada texto como texto (un `=HYPERLINK(...)` no se ejecuta);
no hay `innerHTML` ni `eval`; los enlaces de WhatsApp se arman solo con dígitos.

Encontrado y corregido:

1. **Un carácter nulo (`\u0000`) en cualquier texto devolvía error 500** en todo el sistema (clientes, socios, productos...).
   Ahora cualquier error de datos de la base (texto con caracteres no permitidos, demasiado largo, número fuera de
   rango) responde 400 con un mensaje claro.
2. Una rutina con un renglón vacío (`null`) en la lista provocaba un error interno.
3. Una rutina aceptaba miles de renglones: se puso un tope de 150.
4. Fechas absurdas (año 9999 o 0001) en el resumen provocaban un error interno: ahora se rechazan (años 2000 a 2100).
5. **Vite 8.2.1 (el servidor del frontend en Docker) tenía fallos que permitían leer archivos fuera del proyecto**,
   y Docker lo publicaba a toda la red. Se actualizó a 8.3.5 (0 vulnerabilidades en `npm audit`).
6. **Docker publicaba la base de datos (con la clave `password`), la API y el frontend a toda la red.** Ahora los tres
   puertos solo escuchan en `127.0.0.1`: funciona igual en esa PC, pero nadie más de la red puede conectarse.
   Para abrirlo a otras PC a propósito hay que sacar `127.0.0.1:` de los puertos en `docker-compose.yml`.

Cubierto con tests automáticos (`RobustezTests` y los tests de cada módulo).

## Tercera revisión (validaciones, formularios, acceso y auditoría del equipo)

Un atacante automático lee el Swagger de la API y la ataca endpoint por endpoint, una propiedad por vez, con inyección
SQL (incluida la ciega, con `pg_sleep`), scripts, fórmulas de Excel, caracteres de control, textos de 70.000 caracteres,
números extremos, tipos equivocados y cuerpos rotos: **8.631 pedidos** contra los 94 endpoints. Resultado antes y
después de los cambios de esta revisión:

| | Antes | Después |
|---|---|---|
| Errores internos (5xx) | 0 | 0 |
| Fugas de detalles internos (SQL, trazas) | 0 | 0 |
| Inyección SQL ciega (respuestas lentas) | 0 | 0 |
| Pedidos hostiles aceptados (2xx) | ~940 | ~650 |
| Controles de integridad contable después del ataque | 16 de 16 | 16 de 16 |

(Los 2 o 3 cortes de conexión que aparecen son las URL de 420 KB: Kestrel las rechaza por su límite de línea de pedido.)

Lo que no es un problema: el texto libre (nombres, marcas, sabores) acepta cualquier texto porque todo se guarda con
parámetros (el único SQL manual del backend, `FromSqlInterpolated`, recibe parámetros tipados) y se muestra escapado
(el frontend no usa `innerHTML`, `eval` ni `dangerouslySetInnerHTML`). Bloquear `<` o `'` en los nombres sería una lista
negra, que siempre se esquiva y rechaza nombres legítimos.

Encontrado y corregido:

1. **Las facturas cargadas a mano o importadas (circuito viejo, `POST /api/ventas`, `POST /api/compras` y
   `POST /api/ventas/import`) no validaban formato**: aceptaban un tipo de factura inventado, un número de factura o un CUIT con
   texto libre, una condición de pago fuera de rango (`-1`, `2147483647`), un cliente `-1` y cualquier cantidad de ítems.
   Ahora el dominio exige tipo A, B o C, número `0001-00000001`, CUIT o DNI válido, fecha entre 2000 y 2100, importes coherentes
   (el total es neto más IVA) y de 1 a 200 ítems. La pantalla no usa ese circuito (el Libro de Ventas y Facturar usan
   `/api/facturas`); queda protegido igual porque solo se alcanza llamando a la API a mano.
2. **La subida de Excel no tenía topes**: un `.xlsx` es un zip, y una bomba zip de unos KB que se descomprime a gigas
   agotaría la memoria. Ahora solo se aceptan `.xlsx` de hasta 5 MB, se descomprime de verdad hasta un tope de 50 MB (el
   tamaño que declara el zip puede mentir), se limitan las entradas y las filas, y las filas inválidas se omiten y se cuentan.
3. **Planes, productos y cuentas aceptaban valores sin sentido**: un plan de 2.147.483.647 meses (que rompería al cobrar),
   calorías negativas, precios de 10^20, stock de 2.000 millones y un código de cuenta con `' OR '1'='1`. Ahora tienen tope y forma.
4. **Un email con un carácter nulo (`\0`) en el login llegaba hasta la consulta a la base** y daba un error de
   PostgreSQL (traducido a 400 por la defensa general, pero distinto al resto del login). Ahora se corta en la entrada.
5. **El login permitía descubrir qué emails existen midiendo el tiempo**: con un email inexistente no se calculaba el hash
   y la respuesta era mucho más rápida. Ahora se calcula igual. Además, el registro de cuentas con fallos no puede crecer sin
   límite si alguien prueba miles de emails distintos.
6. **Cargar un archivo de configuración aplicaba cualquier dato del archivo**, sin mostrarlo ni validarlo (`{ ...config, ...archivo }`): una clave inventada o un valor del tipo equivocado (una alícuota de «abc», un tiempo de inactividad negativo) quedaba guardado en el navegador. Ahora se lee con una lista de datos conocidos y su forma (tipo, largo, rango, formato), se muestra una **vista previa** que compara la configuración actual con la del archivo y solo se carga lo válido, al confirmar. Las claves peligrosas como `__proto__` se ignoran.

Agregado:

- **Auditoría del equipo.** La tabla de auditoría guarda la IP, el sistema operativo y el navegador desde los que se hizo
  cada cambio; la API se los informa a PostgreSQL en cada conexión y el trigger los registra (recortados, para que un dato
  de equipo jamás pueda hacer fallar el cambio que se audita). Los inicios de sesión correctos, fallidos y sobre cuentas
  bloqueadas también quedan registrados (tabla lógica `acceso`), con el email que se probó y nunca la contraseña. La IP
  real detrás del proxy se toma de `X-Forwarded-For` **solo** si el pedido llega de una red de confianza
  (`Proxy:RedesDeConfianza`; por defecto, las redes privadas y la propia máquina), con lo que nadie puede falsearla con el
  encabezado ni esquivar el límite de intentos de login por IP.
- **Comparación de copias.** `backup.ps1` guarda un manifiesto con la versión del sistema, la migración de la base y la huella
  de los datos de cada tabla; `comparar-backup.ps1` lo contrasta con el sistema actual y `restaurar.ps1` se niega a restaurar
  una copia de una versión más nueva que el código.

Límites que conviene tener presentes:

- El sistema operativo y el navegador salen del `User-Agent`, que el propio equipo informa y puede falsear. Windows 10 y 11
  se ven igual (`Windows 10/11`). Sirve para saber desde dónde se trabajó, no como prueba.
- Dentro de Docker en una sola PC, la IP que se ve para el equipo del alumno es la de la puerta de enlace de Docker (algo
  como `172.18.0.1`), porque el navegador está en la misma computadora. Detrás de un proxy en un servidor se ve la IP real.
- Los intentos que corta el límite por IP (429) llegan antes que el controlador y no se registran en la auditoría.
- El estado del bloqueo de cuentas sigue en memoria: se pierde al reiniciar el backend (ver *Pendiente*).

Cubierto con tests automáticos (`AgenteDeUsuarioTests`, `ValidacionesTests`, `AuditoriaDeEquipoTests` y
`ValidacionesYArchivosTests`).

## Cuarta revisión (copia completa desde el sistema y política de seguridad)

Hasta acá, la copia de seguridad de la pantalla guardaba solo los 19 datos de la configuración de la empresa; los datos contables
(facturas, asientos...) solo se copiaban con scripts. Ahora el sistema descarga, compara y restaura **todas las tablas**, y
eso agrega superficie que había que revisar: un archivo que sube el usuario y que puede reemplazar toda la base.

Cómo se limitó el riesgo (cada punto tiene su test en `CopiasDeSeguridadTests`):

1. **Solo el Administrador.** Descargar, comparar y restaurar dan 401 sin sesión y 403 al Contador y a Recepción.
2. **El archivo no es de confianza.** Se verifica el SHA-256 de cada archivo, la huella de cada tabla y la **firma HMAC-SHA256 del
   manifiesto** (con una clave del servidor: quien edite un CSV y recalcule los SHA-256 no puede recalcular la firma, y una copia sin
   firma o con otra firma no se restaura; si se cambia la clave, las copias anteriores dejan de restaurarse desde la pantalla, ver
   `BACKUPS.md`); se rechaza un `.zip` dañado,
   con archivos de más o de menos, sin manifiesto o que no es una copia. Tope de 50 MB comprimido, 300 MB descomprimido (se cuentan
   los bytes reales: una bomba zip de 310 MB de ceros se corta) y 100 archivos.
3. **El manifiesto no puede elegir tablas.** Los nombres de tabla del manifiesto solo se usan si existen en la base (forma validada y
   pertenencia al catálogo, y siempre entre comillas): un manifiesto con `; DROP TABLE ...` o con otra tabla se rechaza.
4. **Restaurar es todo o nada.** Una sola transacción: vaciar, cargar, reiniciar secuencias y comprobar la huella de cada tabla antes de
   confirmar. Un archivo roto a mitad de la carga deja el sistema como estaba.
5. **No se restaura por error.** Exige escribir `RESTAURAR` (también lo exige el servidor), descarga antes una copia de lo que hay, y
   rechaza una copia de otra versión de la base.
6. **Queda registrado.** Descargar y restaurar se guardan en la auditoría con el usuario y el equipo.
7. **Copias automáticas.** Programarlas, hacer una a pedido, listarlas y descargarlas es solo del Administrador (401 y 403 como las demás).
   La descarga acepta únicamente el nombre exacto `gymstudio_auto_AAAAMMDD_HHMMSS.zip`, validado antes de armar la ruta y otra vez
   después (sin `..`, barras, rutas absolutas, saltos de línea ni dígitos de otros alfabetos), así que no puede salir de su carpeta; la
   retención solo borra archivos con ese nombre exacto. La programación se valida entera en el servidor y en la base. Cada cambio y cada
   copia (`PROGRAMADA`, `AUTOMATICA`, `FALLIDA`, `GUARDADA`) queda en la auditoría. Una falla de la tarea se guarda con un mensaje
   genérico (el detalle va al registro del servidor) y nunca tumba el sistema. Tienen su test en `CopiasAutomaticasServicioTests` y
   `CopiasAutomaticasApiTests`.

Lo que **no** resuelve y conviene tener presente: la copia no está cifrada y trae los hashes de las contraseñas y los datos
personales de los socios, por lo que su custodia depende de quien la guarda. La restauración necesita que la base le dé permisos de
superusuario a la aplicación (lo hace así el Docker de este proyecto); si no, el sistema lo avisa y se restaura con
`restaurar.ps1`.

Agregado: la [**política de seguridad**](POLITICA_DE_SEGURIDAD.md): objetivo, alcance, roles, reglas (cada una dice si la cumple el
sistema o dependen de las personas), gestión de incidentes y limitaciones conocidas. También está en el manual del sistema y se abre
desde *Configuración › Seguridad y Sistema*.

### Asistente por reglas

El asistente no usa internet ni modelos externos y no ejecuta nada que venga escrito en la pregunta: el texto solo se
compara con palabras clave y nunca llega a una consulta SQL. El rol se decide en el servidor con el token (Recepción
no recibe ningún importe, probado rompiendo la restricción a propósito), la pregunta tiene un máximo de 300 caracteres y
las respuestas no repiten el texto del usuario más que normalizado (sin signos), por lo que no pueden contener HTML.

## Quinta revisión (foto y contacto de los entrenadores)

La ficha del entrenador suma una **foto** que sube el usuario (un archivo más que llega al servidor) y datos de contacto, email y WhatsApp,
que después se convierten en los enlaces `mailto:` y `wa.me`. Cómo se limitó el riesgo (cada punto tiene su prueba en
`FotosDeEntrenadorTests`, `EntrenadoresTests` y `ReglasDeFotoTests`):

1. **Solo la gestión sube y quita; todos ven.** Subir y quitar la foto dan 401 sin sesión y 403 a Recepción. Verla exige sesión: un `<img>`
   no puede mandar el token, así que la pantalla la baja con la sesión y la muestra desde un enlace de objeto del navegador.
2. **Un JPEG de verdad, validado en el servidor.** No se confía en la extensión ni en el `Content-Type` que mande el cliente: se mira la
   estructura del archivo (empieza con `FF D8 FF`, termina con `FF D9` y tiene la cabecera de tamaño `SOF`), pesa de 100 a 150 000 bytes y
   mide de 16 a 512 píxeles de lado (una imagen de 20 000 por 20 000 que pesa poco se rechaza sin decodificarla). Un PNG, un SVG con
   script, un HTML, un JPEG cortado o un cuerpo vacío dan 400 y no se guarda nada. El navegador la achica a 256 por 256 antes de subirla,
   pero eso es una comodidad, no la defensa. El cuerpo se lee con tope (160 000 bytes) y la base repite el límite de peso
   (`CK_FotosDeEntrenador_Tamano`).
3. **Se sirve sin que el navegador adivine.** `image/jpeg`, `X-Content-Type-Options: nosniff`, la `Content-Security-Policy` de la API
   (`default-src 'none'`) y `Cache-Control: private, no-cache` con un `ETag` derivado de la fecha de la subida: el navegador la guarda pero
   pregunta antes de usarla, y al reemplazarla nadie sigue viendo la vieja. Es el único endpoint al que el sistema le deja su propio
   `Cache-Control`; el resto sigue con `no-store`.
4. **Sin auditoría de filas, pero con eventos.** La tabla de fotos no tiene disparador de auditoría (copiaría la foto entera en cada
   cambio); subir y quitar quedan en la auditoría como eventos (`FOTO` y `FOTO_BAJA`) con el usuario y el equipo.
5. **El contacto es texto, no código.** El email tiene forma de email (hasta 254 caracteres, sin espacios) y el WhatsApp admite solo
   el código del país y números (desde la séptima revisión; antes admitía también espacios y signos); el espacio duro que trae pegar desde el teléfono se arregla solo. Al armar los
   enlaces, el email se codifica para que no pueda agregar destinatarios ni asunto a un `mailto:` (por ejemplo con `?bcc=`), el enlace de
   WhatsApp sale solo si el número se puede interpretar y se abre con `rel="noopener noreferrer"`.
6. **Entra en la copia de seguridad.** La tabla `FotosDeEntrenador` viaja en la copia y vuelve igual al restaurarla (probado).

Lo que **no** resuelve: la foto es un dato personal que queda en la base y en las copias, que no están cifradas; su custodia depende de
quien las guarda.

## Sexta revisión (horas y presupuesto de los entrenadores)

Se suma dinero al sistema: el valor de la hora de cada entrenador, las horas que cumplió por día y el presupuesto de pago. Cómo se limitó el
riesgo (cada punto tiene su prueba en `HorasDeEntrenadoresTests`, `HorasDeEntrenadorBaseTests`, `EntrenadoresTests` y `ReglasDeHorasTests`):

1. **Solo la gestión.** Las cuatro rutas (ver, guardar, precargar y presupuesto) dan 401 sin sesión y 403 a Recepción. En la lista de
   entrenadores, Recepción recibe siempre `valorHora: null`: lo oculta el servidor, no la pantalla (que además no le muestra las pestañas).
   Recepción tampoco puede cargarlo (403 al editar la ficha).
2. **El valor de cada día no se reescribe hacia atrás.** Un día guarda el valor de la hora de cuando se cargó. Subir el valor de la ficha no cambia
   los días anteriores, y el cliente ni siquiera manda un valor por día: lo pone el servidor.
3. **Dinero en `decimal`, con el redondeo definido.** El valor de la hora admite hasta 2 decimales (más se rechaza, no se redondea en silencio) y
   va de 0 al tope del dinero del sistema. El monto de un día se redondea al centavo con el medio centavo hacia arriba y el total es la suma
   de los días, no un redondeo de la suma.
4. **Fechas y horas validadas en el servidor y en la base.** No se cargan días que todavía no pasaron (el «hoy» es el de Argentina, UTC-3: a las
   22:30 de allá, que en UTC ya es el día siguiente, «hoy» sigue siendo el mismo día). Las horas van de 0 a 24 y de a cuarto de hora; la base
   lo repite con restricciones, tiene un índice único por entrenador y día y no deja borrar a un entrenador que tiene horas.
5. **Todo o nada y con tope.** Guardar valida todo antes de tocar la base y falla entero (hasta 500 renglones por pedido; períodos de hasta 62
   días): un pedido malo o enorme no deja datos a medias ni consulta un rango sin límite.
6. **Queda registrado.** Cada día de horas que se carga, se corrige o se borra queda en la auditoría por el disparador de la tabla, con el usuario
   y el equipo; el cambio del valor de la hora, por el de la ficha. Las horas entran en la copia de seguridad.
7. **El PDF se arma en el navegador** con lo que contesta el servidor: no hay archivos nuevos que se suban ni se guarden.

Lo que **no** es: el presupuesto es una cuenta de horas por valor de la hora, para estimar un pago. No es una liquidación de sueldos ni genera
asientos contables.

## Séptima revisión (nombres y teléfonos de las personas)

Los nombres de personas y los teléfonos eran texto libre: un nombre podía llevar `<script>`, una fórmula de planilla (`=HYPERLINK(...)`) o un
número que en realidad era otra cosa, y un teléfono sin país no servía para armar el enlace de WhatsApp. Ahora, en entrenadores, socios y
usuarios (pruebas en `ReglasDePersonasTests`, `NombreYTelefonoDePersonasTests`, `EntrenadoresTests` y `telefono.test.ts`):

1. **El servidor es el que manda.** La pantalla filtra lo que se escribe, pero la regla vive en el dominio (`ReglasDePersonas`) y vale por
   cualquier camino: un pedido directo a la API con `Ana2`, `<b>Ana</b>` o `=HYPERLINK(...)` en un nombre, o con un teléfono sin país, recibe
   400 con el motivo y no cambia nada.
2. **Lista permitida, no lista prohibida.** El nombre empieza con una letra (de cualquier alfabeto) y sigue con letras, tilde suelta,
   espacios, puntos, apóstrofos y guiones; el teléfono es `+código número` con solo los dígitos 0-9 (los dígitos de otros alfabetos no valen).
   Con eso no entran signos de HTML, comillas, `;` ni `=` al inicio, que son los que se usan para inyectar o para fórmulas de planilla.
3. **Espacios raros.** El espacio duro, el ideográfico y el separador de línea que trae pegar desde Word o WhatsApp pasan a un espacio
   común antes de validar; el tabulador y el salto de línea se rechazan.
4. **Largos y topes.** Hasta 100 letras en nombre y apellido de socios, 150 en usuarios y 80 en entrenadores; el teléfono, 4 a 14 dígitos y
   hasta 15 con el código. Un largo de más es un 400, no un error de la base.
5. **Datos viejos.** No se migra ni se borra nada: lo ya guardado se sigue mostrando y el formulario lo lleva al formato nuevo al editar. Un
   texto viejo que no es un solo número (por ejemplo dos teléfonos) se muestra vacío en el formulario en vez de juntar sus dígitos en
   un número inventado.
6. **Las banderas no vienen de internet.** Son archivos SVG del paquete `country-flag-icons` (MIT) dentro de la aplicación: no hay pedidos a
   sitios externos al abrir el selector.

Lo que **no** cambia: los nombres de empresa (razón social de clientes y proveedores) siguen siendo texto libre, porque llevan números y
puntos; ahí rigen los topes de largo y la ausencia de caracteres de control de siempre.

## Octava revisión (prueba de intrusión del 9/11/2026)

Se atacó una copia del sistema (base aparte, usuarios de ejemplo) con pedidos armados a mano contra los 115 endpoints: sin sesión, con cada
rol, con tokens manipulados, texto hostil, archivos tramposos y pedidos simultáneos.

**Resistió:** sin sesión todo da 401; un token con `alg=none`, con el rol cambiado, firmado con otra clave o vencido se rechaza; la baja de
un usuario corta su sesión al instante; no hubo inyección SQL ni errores 500, y los errores no muestran detalles internos; el HTML cargado
en clientes y productos se muestra como texto; la foto, las copias (rutas que salen de la carpeta, zip hostil, archivo que no es zip) y CORS
rechazaron todo; `npm audit` y `dotnet list package --vulnerable` no informan vulnerabilidades.

**Se encontró y se corrigió** (pruebas en `CarrerasYClaveDeSesionTests`):

1. **La clave de las sesiones estaba en el repositorio** (`appsettings.Development.json`). Con ella se pudo fabricar un token de
   Administrador sin conocer ninguna contraseña. Ahora no viaja con el código: se configura por variable de entorno o cada instalación
   genera la suya al azar la primera vez (`.secretos/jwt.key`, fuera de git). El token fabricado con la clave vieja da 401.
2. **Cobros simultáneos de una cuota.** Ocho pedidos a la vez generaban ocho facturas y ocho ingresos de caja, y la cuota avanzaba un solo
   mes. Ahora los cobros de un mismo socio pasan de a uno (se toma la fila del socio dentro de la transacción) y un segundo cobro dentro
   de los 10 segundos se rechaza con el motivo: ocho pedidos a la vez dejan una sola factura.
3. **DNI repetido.** La unicidad del DNI se controlaba solo en la aplicación: ocho altas a la vez creaban ocho socios iguales. Ahora la
   garantiza un índice único en la base (`ux_socios_dni`); la migración avisa qué DNI están repetidos si ya había alguno, sin borrar nada.

**Se encontró y queda como está, a decidir:**

- `POST /api/Facturas` no exige rol de gestión: Recepción puede facturar a cualquier precio y cargar compras (queda en la auditoría).
- Cinco claves malas bloquean una cuenta 15 minutos: protege contra la fuerza bruta, pero permite dejar afuera a un usuario a propósito.
- Entrando directo al puerto de la API (no por el frontend) se acepta `X-Forwarded-For`: se esquiva el límite de intentos por IP y la IP de
  la auditoría se puede falsear. Hoy ese puerto solo escucha en la propia PC.
- Recepción puede leer Balance, Diario, Mayor, Caja y Banco aunque el Resumen le oculte los importes, y puede eliminar socios sin historial.

## Novena revisión (cambio de contraseña, campos numéricos y auditoría por páginas)

- **Cambiar mi contraseña** (`PUT /api/auth/mi-clave`): cualquier usuario cambia la suya dando la **actual** y la nueva **dos veces**.
  Comparte el límite de intentos del login (para que no sirva para adivinar la actual), exige la política de contraseñas y que la nueva
  sea distinta de la actual. Queda en la auditoría como `CAMBIO_CLAVE` o, si la actual estaba mal, `CLAVE_RECHAZADA` (además del
  `UPDATE` de `Usuario` con el campo «Contraseña», sin el hash). Pruebas en `CambioDeClaveTests`.
- **Usuarios**: el Administrador puede ponerle una contraseña nueva a otro usuario **sin saber la actual** (para restablecer una
  olvidada), pero tiene que escribirla dos veces: el servidor rechaza el alta o la edición si `confirmacionPassword` no coincide.
- **Campos numéricos**: los importes, precios, cantidades, series, descansos, días, DNI, CUIT y puntos de venta solo dejan escribir lo
  que admiten (`frontend/src/utils/entradaNumerica.ts`, con pruebas). Las **repeticiones** de las rutinas son un número o un rango de
  menor a mayor (`12`, `8-10`); lo exige también el dominio (`RutinaEjercicio.NormalizarRepeticiones`). La migración
  `RepeticionesNumericas` pasó al formato nuevo las que estaban guardadas con texto: el número queda en las repeticiones y el texto
  original en las indicaciones («30 seg» queda 30, con la indicación «30 segundos»). Pruebas en `MigracionDeRepeticionesTests`.
- **Auditoría por páginas** (`GET /api/auditoria?pagina=&tamano=`, total en `X-Total-Count`, hasta 100 por página): la pantalla ya no
  trae 200 registros de una vez. Pruebas en `AuditoriaPaginadaTests`.

## Pendiente antes de usarlo con datos reales

- Cambiar la contraseña del administrador inicial (`jefe@gym.com`, clave de desarrollo). La clave de las sesiones ya no está en el
  repositorio (ver la octava revisión).
- Ejecutar en modo Production: así Swagger no queda publicado. Hoy el Docker corre en modo Development para la demostración.
- Usar HTTPS (con un proxy como Nginx o Caddy) y cambiar la clave de PostgreSQL (`password` en `docker-compose.yml`). El puerto 5433 ya solo escucha en esta PC.
- El token se guarda en `localStorage`, como en la mayoría de las aplicaciones de una sola página. No hay `innerHTML` ni `eval` en el código, pero un XSS permitiría robarlo. La alternativa más segura es una cookie `HttpOnly`.
- Guardar el estado del bloqueo de cuentas en la base si el sistema corre en más de un servidor (hoy está en memoria).
