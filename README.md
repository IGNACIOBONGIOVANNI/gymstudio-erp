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

### Copias de seguridad

Los datos viven en el volumen de Docker: se pierden con `docker compose down -v` o con la PC.
Para copiarlos y restaurarlos (en PowerShell, con el sistema levantado):

```powershell
.\scripts\backup.ps1                # copia en la carpeta backups\ (verificada, con huella SHA-256)
.\scripts\programar-backup.ps1      # la hace sola todos los días
.\scripts\restaurar.ps1 -Archivo .\backups\gymstudio_AAAAMMDD_HHMMSS.dump -Confirmar
```

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

## Roles y permisos

Toda la API exige iniciar sesión, salvo `POST /api/auth/login`. Los permisos se
controlan en el backend; el frontend sólo oculta menús.

| Acción                                                    | Administrador | Contador | Recepción |
|-----------------------------------------------------------|:---:|:---:|:---:|
| Consultar libros, facturar, socios, planes, stock          | ✔ | ✔ | ✔ |
| Anular o editar facturas                                   | ✔ | ✔ | ✘ |
| Crear, confirmar o anular asientos manuales; crear cuentas | ✔ | ✔ | ✘ |
| Administrar usuarios y consultar la auditoría              | ✔ | ✘ | ✘ |

El administrador principal no puede eliminarse, desactivarse ni perder el rol.

Otras medidas: límite de intentos de login por IP (`Auth:IntentosLoginPorMinuto`,
10 por defecto), CORS restringido a los orígenes de `Cors:OrigenesPermitidos` y
sesión de 8 horas (`Jwt:ExpiracionMinutos`).

## Anulación de facturas (trazabilidad contable)

Las facturas y los asientos confirmados **no se borran**:

- **Anular** una factura la marca como anulada y genera un contraasiento, el
  movimiento inverso de caja/banco y la devolución de stock.
- **Editar** una factura revierte los efectos de la versión anterior y aplica los
  de la nueva.
- En el Libro Diario, "anular" un asiento borrador lo elimina; uno confirmado
  genera su contraasiento (o anula la factura a la que pertenece).

## Caja y Banco

- Cada movimiento cargado en el Libro Caja o Banco genera su asiento contra la
  cuenta elegida (Otros Ingresos, Gastos Generales, Deudores por Ventas o
  Proveedores). Editar o eliminar registra un contraasiento.
- **Transferir a Banco / Transferir a Caja**: egreso en un libro, ingreso en el
  otro y un único asiento. No se edita; si hay un error se elimina (se borran los
  dos movimientos) y se carga de nuevo.
- Caja y Banco no se usan en asientos manuales: son cuentas de control cuyo saldo
  sale sólo de sus libros y de las facturas, así el Libro Caja siempre coincide con
  el Mayor.

## Base de datos

Triggers (partida doble, inmutabilidad de asientos, auditoría), restricciones
`CHECK`, vista del Libro Mayor y función de sumas y saldos: ver
[docs/BASE_DE_DATOS.md](docs/BASE_DE_DATOS.md). El esquema completo está en
[01_esquema_base_datos.sql](01_esquema_base_datos.sql).

## Tests

```bash
dotnet test backend/EstudioContable.slnx
```

- **Dominio** (`backend/tests/EstudioContable.Domain.Tests`, sin base de datos):
  partida doble, contraasientos, CUIT, DNI, renovación de membresías, stock,
  movimientos de caja y banco, usuarios.
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

`appsettings.json` deja vacía la clave JWT a propósito: fuera del entorno
`Development` hay que definirla por variable de entorno (mínimo 32 caracteres),
y no configurar `UsuariosIniciales:Password`.

```bash
Jwt__Key=<clave-secreta-larga>
```
