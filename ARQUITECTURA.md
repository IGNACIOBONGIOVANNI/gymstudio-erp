# ERP Contable — Arquitectura Inicial

Stack: **.NET 10 Web API + PostgreSQL/EF Core** (backend) — **React + TypeScript + Tailwind** (frontend)

---

## 1. Estructura del Backend (Clean Architecture / Arquitectura en Capas)

Se recomienda **Clean Architecture** en lugar de una arquitectura en capas "tradicional" (Controller → Service → Repository plano) porque:

- Aísla las **reglas de negocio contables** (balance Debe/Haber, validación de CUIT, etc.) del framework web y de EF Core, en `Domain`.
- Permite testear la lógica de negocio sin levantar base de datos.
- Facilita cambiar de PostgreSQL a otro motor, o de Web API a otro tipo de entrada (colas, gRPC), sin tocar el dominio.

```
EstudioContable.sln
│
├── src/
│   ├── EstudioContable.Domain/              # Núcleo — sin dependencias externas
│   │   ├── Entities/                        # Cliente, Usuario, AsientoContable, LineaAsiento...
│   │   ├── Enums/                           # RolUsuario, EstadoAsiento, TipoContribuyente...
│   │   ├── Exceptions/                      # AsientoDesbalanceadoException, etc.
│   │   └── Interfaces/                      # IClienteRepository, IAsientoRepository (contratos)
│   │
│   ├── EstudioContable.Application/         # Casos de uso — depende sólo de Domain
│   │   ├── Clientes/
│   │   │   ├── Commands/                    # CrearClienteCommand + Handler
│   │   │   ├── Queries/                     # ObtenerClientesQuery + Handler
│   │   │   └── Validators/                  # FluentValidation
│   │   ├── Asientos/
│   │   │   ├── Commands/                    # CrearAsientoCommand, ConfirmarAsientoCommand
│   │   │   ├── Queries/
│   │   │   └── Validators/
│   │   ├── Common/
│   │   │   ├── DTOs/                        # ClienteDto, AsientoDto, LineaAsientoDto
│   │   │   ├── Mappings/                    # AutoMapper Profiles
│   │   │   └── Interfaces/                  # IAppDbContext, ICurrentUserService
│   │   └── DependencyInjection.cs
│   │
│   ├── EstudioContable.Infrastructure/      # Implementaciones concretas
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Configurations/              # IEntityTypeConfiguration<T> por entidad
│   │   │   ├── Migrations/                  # Migraciones EF Core
│   │   │   └── Repositories/                # ClienteRepository, AsientoRepository
│   │   ├── Identity/                        # ASP.NET Core Identity + JWT
│   │   ├── Copias/                          # CopiasDeSeguridadService, CopiasAutomaticasService (guarda, rota y lista)
│   │   └── DependencyInjection.cs
│   │
│   └── EstudioContable.API/                 # Punto de entrada HTTP
│       ├── Controllers/
│       │   ├── ClientesController.cs
│       │   ├── AsientosController.cs
│       │   └── AuthController.cs
│       ├── Middlewares/                     # ExceptionHandlingMiddleware (mapea excepciones
│       │                                     # de dominio a códigos HTTP), Logging
│       ├── Filters/                         # [Authorize(Roles = "Administrador,Contador")]
│       ├── Copias/                          # CopiasAutomaticasHostedService: tarea en segundo plano (cada minuto, ¿toca copia?)
│       ├── Program.cs
│       └── appsettings.json
│
└── tests/
    ├── EstudioContable.Domain.Tests/        # Tests unitarios puros (ej: balance de asientos)
    ├── EstudioContable.Application.Tests/   # Tests de casos de uso (con mocks/InMemory)
    └── EstudioContable.API.Tests/           # Tests de integración (WebApplicationFactory)
```

**Flujo de una request típica** (ej. confirmar un asiento):
`AsientosController` → `Application.Asientos.Commands.ConfirmarAsientoCommand` (vía MediatR, opcional pero recomendado) → carga el agregado `AsientoContable` desde `IAsientoRepository` → llama a `asiento.Confirmar()` (el dominio valida el balance) → `AppDbContext.SaveChangesAsync()` (segunda validación) → 200 OK o excepción mapeada a 422.

### Autenticación y roles

- **ASP.NET Core Identity** + **JWT Bearer** para emitir tokens tras login.
- Claim `role` en el token con uno de: `Administrador`, `Contador`, `Cliente`.
- Autorización por rol a nivel de controller/endpoint: `[Authorize(Roles = "Administrador,Contador")]`.
- Para el rol `Cliente`: además del rol, filtrar siempre por `ClienteId` del token (un cliente sólo debe ver sus propios asientos) — implementarlo como política de autorización o filtro de query, **nunca confiar en un `clienteId` que venga del frontend**.
- Passwords: hash con el `PasswordHasher` de Identity (PBKDF2) — nunca reinventar esto.
- Refresh tokens con expiración corta del access token (ej. 15 min) + rotación de refresh token.

---

## 2. Estructura del Frontend (React + TypeScript + Tailwind)

```
frontend/
├── src/
│   ├── api/
│   │   ├── httpClient.ts            # instancia de axios/fetch con interceptores (JWT, refresh, 401)
│   │   ├── clientesApi.ts
│   │   ├── asientosApi.ts
│   │   └── authApi.ts
│   │
│   ├── features/                    # organización por dominio de negocio, no por tipo de archivo
│   │   ├── clientes/
│   │   │   ├── components/          # ClienteForm, ClienteCard
│   │   │   ├── hooks/                # useClientes, useCrearCliente (TanStack Query)
│   │   │   ├── types.ts
│   │   │   └── ClientesPage.tsx
│   │   ├── asientos/
│   │   │   ├── components/          # AsientoForm, LineaAsientoRow, BalanceIndicator
│   │   │   ├── hooks/
│   │   │   ├── types.ts
│   │   │   └── AsientosPage.tsx
│   │   └── auth/
│   │       ├── components/          # LoginForm
│   │       ├── hooks/                # useAuth
│   │       └── AuthContext.tsx
│   │
│   ├── components/ui/               # componentes shadcn/ui (Button, Input, Dialog, Table...)
│   ├── layouts/                     # AppLayout, AuthLayout (sidebar, header)
│   ├── routes/                      # definición de rutas + guards por rol (RequireRole)
│   ├── store/                       # estado global liviano (Zustand) si hace falta
│   ├── lib/                         # utils.ts, formatCurrency.ts, cn.ts
│   ├── types/                       # tipos compartidos (Rol, EstadoAsiento...)
│   ├── App.tsx
│   └── main.tsx
├── tailwind.config.ts
└── package.json
```

### Librerías recomendadas

| Necesidad | Librería | Por qué |
|---|---|---|
| Tablas de datos complejas (libro diario, listado de clientes) | **TanStack Table** (headless) + **shadcn/ui** para los estilos | TanStack Table maneja sorting, filtros, paginación y **virtualización** (crítico si un libro diario tiene miles de filas) sin imponer estilos; shadcn/ui te da los componentes visuales (Table, Dialog, DropdownMenu) ya integrados con Tailwind y totalmente personalizables (el código vive en tu repo, no es una dependencia de caja negra) |
| Data fetching / caché / invalidación | **TanStack Query** | Maneja loading/error states, refetch automático, caché — evita "useEffect + useState" manual para cada request |
| Formularios | **React Hook Form** + **Zod** | Validación de formularios (ej. CUIT, montos) con schemas reutilizables entre el form y (si se comparte tipo) el contrato con el backend |
| Formateo de moneda | **Intl.NumberFormat** nativo (`es-AR`, `currency: 'ARS'`) | Evita librerías extra sólo para formatear montos |
| Manejo de fechas | **date-fns** | Liviano, tree-shakeable, evita bugs de zona horaria de `Date` nativo si se usa con cuidado |

### Precisión numérica en el frontend

- Los montos llegan del backend como `string` o `number` con 2 decimales — **nunca hacer aritmética de sumas con floats en el cliente para mostrar totales "reales"**; si hace falta sumar en el cliente (ej. total en vivo mientras se carga un asiento), usar una librería de precisión decimal (`decimal.js` o `big.js`) o, mejor, recalcular siempre contra el backend antes de confirmar.
- El `BalanceIndicator` (Debe vs Haber en tiempo real mientras el contador carga un asiento) es un buen candidato para usar `decimal.js` dado que es aritmética visible al usuario antes de guardar.

---

## 3. Notas de seguridad y rendimiento transversales

- **Seguridad**
  - Todas las queries multi-tenant (por cliente) deben filtrar por `ClienteId` en el backend, nunca confiar en el ID que manda el frontend para un usuario con rol `Cliente`.
  - Rate limiting en `AuthController` (login) para mitigar fuerza bruta.
  - Logs de auditoría en asientos: quién creó/confirmó/anuló, y cuándo (ya contemplado con `usuario_creador_id`, `fecha_creacion`, `fecha_confirmacion`).
  - Los asientos **Confirmados** no deberían admitir `UPDATE`/`DELETE` de líneas — sólo `Anular()` y crear un asiento de reverso, para mantener trazabilidad contable/legal.

- **Rendimiento**
  - Índices ya incluidos en el script SQL sobre `(cliente_id, fecha)` para listados de libro diario y `(cliente_id, numero_asiento)` para búsquedas puntuales.
  - Paginación server-side (`skip`/`take` o keyset pagination) en los endpoints de listado — no traer miles de asientos al frontend de una vez; combinarlo con la virtualización de TanStack Table.
  - Considerar particionamiento por año fiscal en `lineas_asiento`/`asientos_contables` si el volumen crece mucho (fuera de alcance de la v1, pero dejar la puerta abierta con `fecha` como columna de partición futura).
