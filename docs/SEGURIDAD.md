# Seguridad del sistema

Qué protege el sistema, cómo se comprobó y qué queda pendiente para un uso real.

## Qué hay implementado

| Capa | Medida |
|---|---|
| Autenticación | Inicio de sesión con JWT firmado (HS256, vence a las 8 h). Las contraseñas se guardan con el hash de ASP.NET Identity (PBKDF2). |
| Sesión | En cada pedido se comprueba en la base que el usuario siga activo y con el mismo rol que dice el token: una baja, una eliminación o un cambio de rol rigen al instante. |
| Fuerza bruta | Límite de intentos de login por IP y bloqueo de la cuenta 15 minutos tras 5 claves incorrectas. El mensaje es el mismo para un email inexistente y para una clave incorrecta. |
| Contraseñas | Mínimo 8 caracteres con letras y números (se exige en el servidor, no solo en la pantalla). |
| Permisos | Toda la API exige sesión salvo el login. Tres roles: Administrador, Contador y Recepción. Recepción factura, cobra cuotas y gestiona socios; no mueve dinero (Caja, Banco, transferencias), no anula, no borra catálogos ni administra usuarios. |
| Navegador | CORS limitado a los orígenes del frontend. Cabeceras `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Cache-Control: no-store` y `Content-Security-Policy` en la API. |
| Datos | Consultas parametrizadas (EF Core). Restricciones en la base: partida doble, inmutabilidad de asientos, tabla de auditoría de solo lectura y auditoría automática de quién cambió qué. |
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

## Pendiente antes de usarlo con datos reales

- Cambiar la contraseña del administrador inicial (`jefe@gym.com`, clave de desarrollo) y la clave `Jwt:Key` de `appsettings.Development.json`.
- Ejecutar en modo Production: así Swagger no queda publicado. Hoy el Docker corre en modo Development para la demostración.
- Usar HTTPS (con un proxy como Nginx o Caddy) y no exponer el puerto de PostgreSQL (5433).
- El token se guarda en `localStorage`, como en la mayoría de las aplicaciones de una sola página. No hay `innerHTML` ni `eval` en el código, pero un XSS permitiría robarlo. La alternativa más segura es una cookie `HttpOnly`.
- Guardar el estado del bloqueo de cuentas en la base si el sistema corre en más de un servidor (hoy está en memoria).
