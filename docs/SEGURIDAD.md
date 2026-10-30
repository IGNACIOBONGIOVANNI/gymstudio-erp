# Seguridad del sistema

Qué protege el sistema, cómo se comprobó y qué queda pendiente para un uso real.

## Qué hay implementado

| Capa | Medida |
|---|---|
| Autenticación | Inicio de sesión con JWT firmado (HS256, vence a las 8 h). Las contraseñas se guardan con el hash de ASP.NET Identity (PBKDF2). |
| Sesión | En cada pedido se comprueba en la base que el usuario siga activo y con el mismo rol que dice el token: una baja, una eliminación o un cambio de rol rigen al instante. |
| Fuerza bruta | Límite de intentos de login por IP y bloqueo de la cuenta 15 minutos tras 5 claves incorrectas. El mensaje es el mismo —y tarda lo mismo— para un email inexistente y para una clave incorrecta. Emails y claves absurdos se rechazan sin consultar la base. |
| Contraseñas | Mínimo 8 caracteres con letras y números (se exige en el servidor, no solo en la pantalla). |
| Permisos | Toda la API exige sesión salvo el login. Tres roles: Administrador, Contador y Recepción. Recepción factura, cobra cuotas y gestiona socios; no mueve dinero (Caja, Banco, transferencias), no anula, no borra catálogos ni administra usuarios. |
| Navegador | CORS limitado a los orígenes del frontend. Cabeceras `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Cache-Control: no-store` y `Content-Security-Policy` en la API. |
| Datos | Consultas parametrizadas (EF Core). Restricciones en la base: partida doble, inmutabilidad de asientos, tabla de auditoría de solo lectura y auditoría automática de quién cambió qué. |
| Validaciones | Los números, códigos, tipos y documentos tienen forma y tope en el dominio (valen por cualquier camino). El texto libre no admite caracteres de control ni largos absurdos. |
| Archivos | Solo `.xlsx` de hasta 5 MB; se descomprime hasta un tope (bomba zip) y se limitan las filas. |
| Auditoría del equipo | Cada cambio y cada inicio de sesión registran IP, sistema operativo y navegador. La IP solo se toma de `X-Forwarded-For` desde una red de confianza. |
| Copias | SHA-256 y manifiesto por copia; `comparar-backup.ps1` la contrasta con el sistema actual y `restaurar.ps1` se niega a restaurar una versión más nueva. |
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
4. **Un email con un carácter nulo (` `) en el login llegaba hasta la consulta a la base** y daba un error de
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

### Asistente por reglas

El asistente no usa internet ni modelos externos y no ejecuta nada que venga escrito en la pregunta: el texto solo se
compara con palabras clave y nunca llega a una consulta SQL. El rol se decide en el servidor con el token (Recepción
no recibe ningún importe, probado rompiendo la restricción a propósito), la pregunta tiene un máximo de 300 caracteres y
las respuestas no repiten el texto del usuario más que normalizado (sin signos), por lo que no pueden contener HTML.

## Pendiente antes de usarlo con datos reales

- Cambiar la contraseña del administrador inicial (`jefe@gym.com`, clave de desarrollo) y la clave `Jwt:Key` de `appsettings.Development.json`.
- Ejecutar en modo Production: así Swagger no queda publicado. Hoy el Docker corre en modo Development para la demostración.
- Usar HTTPS (con un proxy como Nginx o Caddy) y cambiar la clave de PostgreSQL (`password` en `docker-compose.yml`). El puerto 5433 ya solo escucha en esta PC.
- El token se guarda en `localStorage`, como en la mayoría de las aplicaciones de una sola página. No hay `innerHTML` ni `eval` en el código, pero un XSS permitiría robarlo. La alternativa más segura es una cookie `HttpOnly`.
- Guardar el estado del bloqueo de cuentas en la base si el sistema corre en más de un servidor (hoy está en memoria).
