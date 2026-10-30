# Política de seguridad de la información — GymStudio ERP

| | |
|---|---|
| Versión del documento | 1.0 |
| Fecha | octubre de 2026 |
| Aplica a | GymStudio ERP 1.0.0 |
| Responsable | La persona con rol **Administrador** del sistema |

Este documento fija **qué se protege, quién es responsable de qué y qué reglas se cumplen**. Cada regla indica quién la hace
cumplir: **Sistema** (el programa la exige solo, aunque alguien quiera saltearla) o **Persona** (depende de quien administra
o usa el sistema). El detalle técnico de cada medida y de las revisiones hechas está en [`SEGURIDAD.md`](SEGURIDAD.md); la forma
de usar las copias, en [`BACKUPS.md`](BACKUPS.md). El mismo resumen está en el manual, sección *Política de seguridad*.

## 1. Objetivo

Proteger la **confidencialidad**, la **integridad** y la **disponibilidad** de la información del negocio:

- **Confidencialidad:** cada persona ve y hace solo lo que su trabajo necesita. Los datos personales de los socios (nombre,
  DNI, teléfono, email) y los importes del negocio no se muestran a quien no los necesita.
- **Integridad:** los datos contables no se alteran sin dejar rastro. Un asiento confirmado no se edita; una factura
  equivocada se anula, no se borra; todo cambio queda registrado con quién, cuándo y desde qué equipo.
- **Disponibilidad:** el negocio puede seguir trabajando y recuperarse de un error, una rotura de la PC o un ataque, gracias a
  copias de seguridad verificadas.

## 2. Alcance

Rige para todas las personas que usan o administran el sistema (Administrador, Contador y Recepción) y para todo lo que lo
compone: la aplicación web, la API, la base de datos PostgreSQL, las copias de seguridad, los archivos que se suben o se
descargan y la computadora donde se instala.

## 3. Principios

1. **Mínimo privilegio.** Cada rol tiene solo los permisos necesarios. Lo no permitido expresamente está prohibido.
2. **Cerrado por defecto.** Toda la API exige sesión, salvo el inicio de sesión.
3. **Defensa en capas.** Las reglas importantes se aplican en la pantalla, en el servidor **y** en la base de datos; ocultar un
   botón nunca es la única protección.
4. **No se confía en lo que llega.** Todo dato de entrada se valida en el servidor (forma, tipo, largo y rango).
5. **Trazabilidad.** Lo que se cambia se registra, y el registro no se puede editar desde el sistema.
6. **Lo que no se puede deshacer se confirma.** Restaurar una copia exige escribir una palabra y deja una copia previa.

## 4. Roles y responsabilidades

| Rol | Puede | No puede |
|---|---|---|
| **Administrador** | Todo lo del Contador, administrar usuarios, consultar la auditoría y manejar las copias de seguridad. | — |
| **Contador** | Facturar, anular, mover Caja y Banco, ver cuentas a cobrar y a pagar, crear asientos y cuentas, reportes. | Administrar usuarios, ver la auditoría, manejar copias. |
| **Recepción** | Facturar, cobrar cuotas, gestionar socios, ingresos y rutinas. | Ver importes del negocio, mover dinero, anular, borrar catálogos, administrar usuarios. |

**El Administrador** es responsable de: dar de alta y de baja las cuentas, asignar el rol mínimo necesario, revisar la
auditoría, hacer y custodiar las copias de seguridad, mantener el sistema actualizado y atender los incidentes (sección 7).
El administrador principal no puede eliminarse, desactivarse ni perder su rol, para que el sistema nunca quede sin nadie que
lo administre (**Sistema**).

**Toda persona usuaria** debe: usar solo su propia cuenta, no compartir su contraseña, cerrar la sesión al dejar la PC, no
anotar la contraseña a la vista, avisar al Administrador ante cualquier cosa rara (un acceso que no hizo, un dato que cambió
solo) y no sacar del sistema datos personales de los socios para otros fines.

## 5. Reglas

### 5.1 Cuentas y contraseñas

| Regla | Quién la hace cumplir |
|---|---|
| Una cuenta por persona, con email propio. Las cuentas que ya no se usan se dan de baja (se desactivan, no se comparten). | Persona |
| La baja, el cambio de rol y la desactivación rigen **al instante**, aunque la persona tenga la sesión abierta. | Sistema |
| Contraseña de al menos 8 caracteres, con letras y números. Se exige en el servidor, no solo en la pantalla. | Sistema |
| Las contraseñas se guardan con un hash (PBKDF2 de ASP.NET Identity), nunca en texto. | Sistema |
| La contraseña inicial que entrega el Administrador se cambia en el primer uso. La cuenta de ejemplo `jefe@gym.com` y su clave de desarrollo se reemplazan **antes** de cargar datos reales. | Persona |
| Tras 5 contraseñas incorrectas seguidas, la cuenta se bloquea 15 minutos. El ingreso admite 10 intentos por minuto desde una misma PC. | Sistema |
| El mensaje de error (y el tiempo de respuesta) es el mismo para un email inexistente y para una clave incorrecta: no se puede averiguar qué cuentas existen. | Sistema |

### 5.2 Sesiones

| Regla | Quién la hace cumplir |
|---|---|
| La sesión vence a las 8 horas, aunque se esté usando. | Sistema |
| La sesión se cierra sola tras un período de inactividad (60 minutos por defecto, configurable en *Configuración › Seguridad y Sistema*), con aviso previo. | Sistema |
| En una PC compartida se usa un tiempo de inactividad corto y se cierra la sesión al retirarse. «Sin límite» es solo para demostraciones. | Persona |

### 5.3 Permisos y datos

| Regla | Quién la hace cumplir |
|---|---|
| Cada operación valida el rol en el servidor; una llamada directa a la API recibe 401 (sin sesión) o 403 (sin permiso). | Sistema |
| Toda entrada se valida en el servidor: formato, tipo, largo y rango. Las consultas a la base van parametrizadas. | Sistema |
| Los asientos confirmados no se modifican, los asientos cumplen la partida doble y la tabla de auditoría es de solo lectura; lo exige la propia base de datos. | Sistema |
| Los textos no admiten caracteres de control ni largos absurdos; lo que se muestra va escapado (no hay `innerHTML` ni `eval`). | Sistema |
| Solo se aceptan archivos `.xlsx` de hasta 5 MB y copias `.zip` de hasta 50 MB, con límites de descompresión y de filas. | Sistema |
| Los errores internos no muestran trazas ni datos de la base. | Sistema |

### 5.4 Registro y auditoría

| Regla | Quién la hace cumplir |
|---|---|
| Todo alta, modificación y baja registra usuario, fecha, tabla, campos y **desde qué equipo** (IP, sistema operativo y navegador). | Sistema |
| Los inicios de sesión correctos, los fallidos y los intentos sobre cuentas bloqueadas quedan registrados; la contraseña nunca. | Sistema |
| Descargar y restaurar una copia de seguridad también quedan registrados. | Sistema |
| El Administrador revisa la auditoría —en especial *Accesos* y *Copias de seguridad*— con regularidad (como mínimo, una vez por semana). | Persona |

### 5.5 Copias de seguridad

| Regla | Quién la hace cumplir |
|---|---|
| Se hace una copia **todos los días** (`scripts\programar-backup.ps1` la programa a las 22:00) y se conservan las últimas 14. | Persona (programada) |
| Antes de actualizar el sistema, de cargar datos masivos o de restaurar, se descarga una copia completa desde *Configuración › Copia de seguridad*. | Persona |
| Al menos una copia se guarda **fuera de la PC** (pendrive o nube privada). Una copia en el mismo disco no sirve si el disco se rompe. | Persona |
| La copia incluye los hashes de las contraseñas y los datos personales de los socios: se guarda en un lugar privado y no se envía por canales abiertos. | Persona |
| Cada copia trae su huella SHA-256 y un manifiesto con la versión, la migración y la huella de cada tabla; se rechaza una copia dañada, alterada o de una versión más nueva. | Sistema |
| Antes de restaurar se compara la copia con el sistema actual (vista previa tabla por tabla). Restaurar exige escribir `RESTAURAR`, descarga antes una copia de lo que hay y se hace en una sola transacción: si algo falla, no queda nada a medias. | Sistema |
| Solo el Administrador puede descargar, comparar y restaurar copias. | Sistema |
| Las copias se **prueban**: una vez por mes se compara una copia con el sistema (o se restaura en una instalación de prueba) para comprobar que sirve. | Persona |

### 5.6 Instalación y red

| Regla | Quién la hace cumplir |
|---|---|
| Los puertos de la base de datos, la API y el frontend solo escuchan en la propia PC (`127.0.0.1`). Abrirlos a la red es una decisión expresa del Administrador. | Sistema (configuración) |
| La IP del equipo solo se toma de `X-Forwarded-For` si el pedido viene de una red de confianza, para que nadie pueda falsearla. | Sistema |
| Con datos reales: ejecutar en modo *Production* (Swagger no queda publicado), cambiar la clave `Jwt:Key` y la clave de PostgreSQL, y publicar solo por HTTPS detrás de un proxy (Nginx o Caddy). | Persona |
| Mantener actualizados el sistema operativo, Docker y las dependencias, y revisar las vulnerabilidades conocidas (`npm audit`, `dotnet list package --vulnerable`). | Persona |

### 5.7 Datos personales

Los datos de los socios son datos personales (Ley 25.326 de Protección de los Datos Personales). Se usan **solo** para gestionar
el gimnasio, se muestran a quien los necesita para su trabajo, no se publican ni se ceden, y la persona puede pedir que se
corrijan. Los listados y las copias que los contienen se tratan con el mismo cuidado que el sistema.

## 6. Qué hace el sistema solo y qué depende de las personas

El sistema aplica por sí mismo los controles técnicos (permisos, validaciones, bloqueo de cuentas, auditoría, verificación de
copias). **No puede**, en cambio, obligar a que se cambie la contraseña inicial, a que la copia diaria se guarde fuera de la PC,
a que se revise la auditoría ni a que nadie le preste su cuenta a otra persona. Esos puntos están marcados como *Persona* y son
los que más incidentes producen en la práctica.

## 7. Gestión de incidentes

Ante una sospecha (un acceso que nadie hizo, datos que cambiaron solos, una PC perdida, una copia extraviada):

1. **Contener.** Desactivar la cuenta involucrada en *Usuarios* (rige al instante) y cambiar su contraseña. Si se sospecha de
   toda la instalación, cambiar `Jwt:Key`: invalida todas las sesiones abiertas.
2. **Investigar.** Revisar la *Auditoría*: qué se hizo, con qué cuenta y desde qué IP, sistema y navegador. Ejecutar el
   *Control de integridad* para ver si algún total dejó de cerrar.
3. **Recuperar.** Si hay datos dañados, comparar la última copia sana con el sistema y, si corresponde, restaurarla (queda una
   copia de lo que había). Después, volver a cargar lo registrado entre la copia y el incidente.
4. **Informar.** Avisar a quien corresponda; si se filtraron datos personales de socios, avisarles.
5. **Aprender.** Anotar qué pasó y qué se cambió para que no se repita (se suma a `SEGURIDAD.md`).

## 8. Limitaciones conocidas

Conviene tenerlas presentes; no se ocultan:

- El sistema **no tiene doble factor de autenticación**.
- El token de sesión se guarda en el navegador (`localStorage`); un XSS permitiría robarlo. El código no usa `innerHTML` ni `eval`.
- El bloqueo de cuentas está en memoria: se pierde si se reinicia el backend, y no se comparte entre varios servidores.
- El sistema operativo y el navegador del registro los informa el propio equipo y pueden falsearse; sirven para saber desde dónde
  se trabajó, no como prueba. Dentro de Docker en una sola PC, la IP que se ve es la de Docker.
- La demostración corre en modo *Development*, con claves de ejemplo. No es la configuración para datos reales (ver 5.6).
- Las copias incluyen datos personales y hashes de contraseñas, pero **no están cifradas**: su custodia es responsabilidad de
  quien las guarda.
- La configuración de la empresa (razón social, CUIT, parámetros) vive en el navegador; se respalda con su propio archivo.

## 9. Revisión

La política se revisa **cada vez que cambia la versión del sistema** y, como mínimo, cada seis meses. Cada revisión de seguridad
queda anotada en [`SEGURIDAD.md`](SEGURIDAD.md). Incumplir una regla marcada como *Persona* es responsabilidad de quien la tenía a
cargo; una regla marcada como *Sistema* que se pueda saltear es un defecto y se corrige.
