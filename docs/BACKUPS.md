# Copias de seguridad

Los datos contables (facturas, asientos, caja, banco, stock, socios, usuarios y auditoría) viven en
PostgreSQL, dentro del volumen de Docker `postgres_data`. Ese volumen **se borra** con
`docker compose down -v`, y se pierde con la PC. Por eso hay dos formas de copiarlos (y la de la pantalla se puede programar para
que se haga sola: ver *Hacerla automática*):

| | Desde el sistema (`.zip`) | Con los scripts (`.dump`) |
|---|---|---|
| Dónde | *Configuración › Copia de seguridad › Copia completa del sistema* | PowerShell, `scripts\backup.ps1` |
| Quién | Solo el Administrador | Quien tenga acceso a la PC |
| Sirve para | Sacar una copia en el momento, compararla con el sistema y restaurarla sin abrir una terminal | La copia diaria programada y la restauración si el sistema no arranca |
| Formato | `.zip` con un archivo `.csv` por tabla y un `manifiesto.json` | Volcado de PostgreSQL (`pg_dump`) con `.sha256` y `.manifiesto.json` |

Las dos incluyen **todas** las tablas. La otra parte, la *Configuración de la empresa* (razón social, CUIT, alícuota), está en el
navegador y no en la base: se respalda con su propio archivo en la misma pestaña (con vista previa antes de cargarlo).

## Desde el sistema: copia completa

En *Configuración › Copia de seguridad › Copia completa del sistema* (solo Administrador):

- **Descargar copia completa (.zip):** genera el archivo en el momento, con una foto consistente de la base (una sola transacción,
  aunque alguien esté facturando). Trae `manifiesto.json` y `datos/<esquema>.<tabla>.csv` por cada una de las tablas (facturas,
  asientos, caja, banco, stock, socios, usuarios y auditoría).
- **Seleccionar copia (.zip):** verifica la copia y muestra una vista previa que la compara con el sistema actual, **tabla por
  tabla** y agrupada por módulo: filas en la copia, filas ahora y si cambia, con la opción de ver solo lo que cambia. No modifica nada.
  La auditoría se muestra aparte («se actualiza sola»): crece con cada acción, hasta con descargar la copia.
- **Restaurar:** desde esa vista previa. Pide escribir `RESTAURAR`, descarga antes una copia de lo que hay
  (`gymstudio_antes-de-restaurar_*.zip`) y reemplaza **todos** los datos.

Qué hace el servidor, y por qué se puede confiar en el archivo:

- El manifiesto trae la **versión del sistema**, la **migración** de la base y, por tabla, la cantidad de filas y la huella MD5 de su
  contenido; cada archivo del `.zip` trae además su SHA-256. Un archivo dañado, alterado, con tablas de más o de menos o que no es una
  copia se rechaza antes de tocar la base.
- El manifiesto va **firmado** (HMAC-SHA256 de todo lo que afirma: versión, migración y, por tabla, filas, huella y SHA-256) con una
  clave del servidor que no está en el archivo. Los SHA-256 solos no alcanzan: quien edite un CSV puede recalcularlos. Sin la clave no
  puede recalcular la firma, así que una copia modificada, de otra instalación con otra clave o sin firma **se compara pero no se restaura**
  desde la pantalla. La clave es `Copias:ClaveDeFirma` y, si no se define, se usa `Jwt:Key`. **Ojo:** si se cambia esa clave (por ejemplo
  al rotar `Jwt:Key` tras un incidente), las copias hechas antes dejan de poder restaurarse desde la pantalla; para evitarlo, definí una
  `Copias__ClaveDeFirma` propia y no la cambies. Las copias `.dump` con `restaurar.ps1` no dependen de esta clave.
- Solo se aceptan nombres de tabla que existen en la base: un manifiesto armado a mano no puede apuntar a otra tabla ni inyectar SQL.
- Límites: el `.zip` de hasta 50 MB y hasta 300 MB ya descomprimido (el tamaño que declara el zip puede mentir, se cuentan los bytes
  reales) y hasta 100 archivos.
- Desde la pantalla solo se restaura una copia **de la misma versión de la base** (misma migración y mismas tablas) que el sistema
  actual. Una de otra versión sí se compara en la vista previa, pero no se puede restaurar; para recuperar datos de una versión
  distinta se usa una copia `.dump` con `restaurar.ps1`, que aplica las migraciones que faltan al arrancar.
- Restaurar es **todo o nada**: vacía las tablas, carga la copia, reinicia las secuencias y verifica la huella de cada tabla dentro
  de una sola transacción. Si algo falla en cualquier punto (un archivo roto a la mitad, una huella que no coincide), se deshace todo
  y el sistema queda como estaba. Al terminar corre el *Control de integridad*.
- Descargar y restaurar quedan en la **Auditoría** (tabla *Copias de seguridad*), con el usuario y el equipo.

La copia incluye los datos de los socios y los hashes de las contraseñas, y no está cifrada: guardala en un lugar privado.

## Con los scripts: hacer una copia

Desde la carpeta del proyecto, con el sistema levantado (`docker compose up`), en PowerShell:

```powershell
.\scripts\backup.ps1
```

Crea `backups\gymstudio_AAAAMMDD_HHMMSS.dump` con dos archivos al lado:

- `.sha256`: la huella SHA-256 del archivo, para detectar que se dañó o que alguien lo modificó;
- `.manifiesto.json`: la **versión del sistema**, la **migración** de la base y una huella de los datos de cada tabla
  (filas y MD5 de su contenido). Con eso se puede comparar la copia con el sistema actual (ver *Comparar con el sistema actual*).

No hace falta instalar nada: usa el `pg_dump` del contenedor de la base. Antes de guardar la copia verifica que se pueda
leer, y si alguien carga datos mientras se copia, repite la copia hasta que la huella de los datos coincida con la del
archivo. Se conservan las últimas 14 (se cambia con `-Conservar 30`; `-Carpeta D:\Respaldos` la guarda en otro lugar).

## Comparar con el sistema actual

```powershell
.\scripts\comparar-backup.ps1 -Archivo .\backups\gymstudio_20261027_142741.dump
```

No modifica nada. Dice, en este orden:

1. **Integridad:** si la huella SHA-256 del archivo es la que se guardó al hacerlo (si no, el archivo está dañado: código de salida 1);
2. **Versión:** la versión del sistema y la migración de la base de la copia contra las de ahora, y contra las migraciones que
   trae el código instalado (una copia de una versión **más nueva** que el código no se puede restaurar tal cual);
3. **Datos:** cuántas tablas cambiaron desde la copia y cuáles (por ejemplo, «la copia tiene 45 filas y ahora hay 46», o «mismas
   12 filas, pero el contenido cambió»).

Termina con un veredicto: la copia es **idéntica** al sistema actual (código 0), es de un estado o de una versión anterior
(código 2) o está dañada (código 1). Sirve para saber si una copia está al día o si conviene hacer otra. Las copias hechas con
la versión anterior de `backup.ps1` no traen manifiesto: en ese caso solo se puede verificar la huella del archivo.

## Hacerla automática

Hay dos caminos y se pueden usar los dos.

### Desde el sistema: copia automática

En *Configuración › Copia de seguridad › Copia automática* (solo Administrador) se programa cada cuánto se hace la copia completa:
**cada 6 horas**, **cada 12 horas**, **todos los días**, **una vez por semana** (a elección, el día) o **una vez por mes** (el día 1 al 28:
el 29, 30 y 31 no existen todos los meses). Se elige la hora (**hora de Argentina**, UTC-3, como el resto del sistema; en las
frecuencias de 6 y 12 horas, desde cuándo se cuenta) y **cuántas copias conservar** (de 1 a 60; por defecto 14). La pantalla muestra la
**próxima copia**, la **última** y, si falló, el motivo; tiene el botón **Hacer una copia ahora** (útil para comprobar que anda sin esperar
a la hora programada) y la lista de las copias guardadas con su botón *Descargar*.

Cómo funciona, y por qué se puede confiar:

- Es una tarea dentro del propio backend: espera unos segundos a que el sistema arranque y, cada minuto, se pregunta si toca. La
  programación vive en la base (tabla `ConfiguracionDeCopias`, una sola fila, con restricciones que repiten las reglas aunque alguien
  escriba directo en ella).
- Hace **la misma copia firmada** que *Descargar copia completa*: no hay un segundo formato. Se compara y se restaura con *Seleccionar
  copia (.zip)* como cualquier otra, y una copia alterada se compara pero no se restaura.
- Se guarda en la carpeta `backups\automaticas` del proyecto, con el nombre `gymstudio_auto_AAAAMMDD_HHMMSS.zip` (hora de Argentina).
  El `docker-compose.yml` monta esa carpeta de la PC dentro del contenedor (`./backups:/backups` y `Copias__Carpeta=/backups/automaticas`):
  sin eso las copias vivirían dentro del contenedor y se perderían al recrearlo. Al actualizar el proyecto hay que **recrear el
  contenedor del backend una vez** (`docker compose up -d`) para que tome el volumen.
- Se escribe primero en un archivo temporal y después se renombra, así no queda nunca un archivo a medias con nombre de copia (un corte
  de luz deja a lo sumo un `.zip.tmp`, que se borra en la corrida siguiente).
- **Retención:** después de guardar una copia se borran las más viejas y quedan las últimas N, pero solo entre los archivos que se llaman
  exactamente como una copia automática: nunca toca los `.dump` de los scripts ni ningún otro archivo de la carpeta.
- **Si Docker estaba apagado** a la hora programada, se hace **una** copia apenas arranca, aunque se hayan salteado varias. Una copia hecha
  con *Hacer una copia ahora* cuenta como la última, pero no mueve la próxima programada.
- **Si falla** (carpeta sin permisos, disco lleno, base ocupada) el sistema lo anota, lo muestra en pantalla con un mensaje claro (el detalle
  técnico va al registro del servidor) y **reintenta a los 30 minutos**, no cada minuto. Una falla nunca tumba el sistema.
- **Seguridad:** solo el Administrador ve, cambia y descarga (401 sin sesión, 403 para Contador y Recepción). La descarga acepta solamente
  el nombre exacto de una copia automática (`gymstudio_auto_` + fecha + hora + `.zip`, validado antes de armar la ruta y otra vez después):
  no hay forma de pedir `..\`, una ruta absoluta ni otro archivo. Cada cambio de programación y cada copia quedan en la **Auditoría**
  (tabla *Copias de seguridad*: `PROGRAMADA` con quién lo cambió, `AUTOMATICA` y `FALLIDA` con el usuario «sistema», `GUARDADA` para las
  hechas a pedido).
- Las copias **no están cifradas** y traen datos de socios y hashes de contraseñas: la carpeta `backups` es privada, igual que las demás.
- Como la programación está en la base, **restaurar una copia devuelve también la programación** a como estaba cuando se hizo esa copia.
  Una copia hecha con una versión anterior de la base se compara pero no se restaura desde la pantalla (comportamiento de siempre).
- Dos opciones de configuración (`appsettings` o variables de entorno): `Copias:Carpeta` (por defecto `backups/automaticas`; en Docker,
  `/backups/automaticas`) y `Copias:TareaAutomatica` (`true` por defecto; con `false` no corre la tarea).

Requiere que el sistema esté levantado (Docker abierto) a esa hora. Si querés una copia aunque el sistema esté apagado, usá el camino
siguiente.

### Con el Programador de tareas de Windows (scripts)

```powershell
.\scripts\programar-backup.ps1            # todos los días a las 22:00
.\scripts\programar-backup.ps1 -Hora 18:30
.\scripts\programar-backup.ps1 -Quitar    # la desactiva
```

Usa el Programador de tareas de Windows; si la PC estaba apagada a esa hora, la copia se hace apenas
se prende. Requiere Docker Desktop abierto. Hace copias `.dump` (volcado de PostgreSQL) en la carpeta `backups`, aparte de las
automáticas del sistema.

## Restaurar

```powershell
.\scripts\restaurar.ps1 -Archivo .\backups\gymstudio_20261027_142741.dump            # solo avisa qué haría
.\scripts\restaurar.ps1 -Archivo .\backups\gymstudio_20261027_142741.dump -Confirmar # restaura
```

Reemplaza **todos** los datos actuales por los de la copia. Antes de tocar nada:

1. verifica la huella SHA-256 del archivo (si está dañado, se detiene) y lo compara con el sistema actual, igual que
   `comparar-backup.ps1`: así se ve qué versión es y qué datos cambiarían;
2. se niega a restaurar una copia de una versión **más nueva** que el código instalado, porque dejaría una base que ese código no
   entiende (con `-Forzar` se restaura igual). Una copia de una versión anterior sí se puede: el sistema aplica las migraciones que
   le faltan al arrancar;
3. comprueba que sea una copia válida de PostgreSQL;
4. hace una copia de lo que hay ahora, `antes-de-restaurar_*.dump`, por si te arrepentís.

Al terminar reinicia el backend.

## Guardarla fuera de la PC

Una copia en la misma PC se pierde con ella. Copiá la carpeta `backups` a un pendrive o a la nube
(Drive, OneDrive) de forma periódica. Las copias traen datos reales, incluidos los hashes de contraseñas
de los usuarios: guardalas en un lugar privado.

## Cómo se probó

**Copia completa desde el sistema:** 23 pruebas automáticas contra una PostgreSQL real (`CopiasDeSeguridadTests`): contenido y huellas
del `.zip`; copia idéntica, con datos cambiados y de otra versión; ida y vuelta (se descarga, se cambian y borran datos, se restaura y
quedan exactamente los de antes, con secuencias, auditoría e integridad); confirmación incorrecta; copia dañada; nombres de tabla
inyectados; tabla desconocida; firma válida, firma que no coincide (atacante que recalcula los SHA-256 sin la clave) y firma quitada; falla a mitad de la carga (se deshace todo); archivo que no es una copia; bomba zip (310 MB de ceros); y
los permisos (401 sin sesión, 403 para Contador y Recepción).

Además se probó la pantalla de punta a punta en un Docker aparte levantado desde el `.zip` del proyecto: se descargó una copia, se
cargó un cliente, la vista previa mostró exactamente esa diferencia («Clientes: 0 en la copia, 1 ahora»), el botón de restaurar siguió
desactivado hasta escribir `RESTAURAR` en mayúsculas, y la restauración devolvió el sistema al estado de la copia (26 tablas,
112 filas, integridad contable 16 de 16, un alta posterior sin choques de numeración y el evento en la auditoría). Con una copia
idéntica al sistema, la vista previa lo dice y no ofrece restaurar.

**Con los scripts**, contra una copia local con el ejercicio de la cátedra (30 facturas):

- se hizo una copia, se cargó una factura, un cliente y un usuario, y se restauró;
- las 21 tablas quedaron con exactamente los mismos registros que antes de alterarlos;
- siguieron funcionando los triggers y restricciones (13 de 13 pruebas de `pruebas_base_de_datos.sql`),
  el inicio de sesión, el saldo de Caja y la emisión de facturas;
- una copia con un byte alterado, un archivo que no es una copia y un archivo inexistente se rechazan
  sin tocar la base.

La comparación y el manifiesto se probaron con Windows PowerShell 5.1 contra una base con datos reales:

- recién hecha, la copia se compara como **idéntica** al sistema (código 0);
- tras cambiar un plan y agregar otro, indica exactamente las tablas que cambiaron (código 2);
- una copia con un byte agregado se rechaza por la huella (código 1);
- una copia con una migración futura en el manifiesto no se restaura (con `-Forzar` sí);
- se restauró una copia y quedó **idéntica** al sistema, con su copia `antes-de-restaurar` aparte.
