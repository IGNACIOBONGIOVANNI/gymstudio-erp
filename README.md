# GymStudio ERP — Sistema contable

Sistema de gestión para un gimnasio: contabilidad por partida doble (Libro Diario,
Mayor, Balance), facturación, caja, banco, stock, socios, planes y membresías.

- **Backend:** .NET 10 Web API · Clean Architecture · MediatR · EF Core · PostgreSQL
- **Frontend:** React 19 · TypeScript · Vite · Tailwind · TanStack Query/Table
- **Autenticación:** JWT + contraseñas hasheadas (PBKDF2, `PasswordHasher` de ASP.NET Identity)

## Cómo levantarlo

### Con Docker

```bash
docker compose up
```

- Frontend: http://localhost:5173
- API + Swagger: http://localhost:5122/swagger

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

## Base de datos

Triggers (partida doble, inmutabilidad de asientos, auditoría), restricciones
`CHECK`, vista del Libro Mayor y función de sumas y saldos: ver
[docs/BASE_DE_DATOS.md](docs/BASE_DE_DATOS.md). El esquema completo está en
[01_esquema_base_datos.sql](01_esquema_base_datos.sql).

## Configuración fuera de desarrollo

`appsettings.json` deja vacía la clave JWT a propósito: fuera del entorno
`Development` hay que definirla por variable de entorno (mínimo 32 caracteres),
y no configurar `UsuariosIniciales:Password`.

```bash
Jwt__Key=<clave-secreta-larga>
```
