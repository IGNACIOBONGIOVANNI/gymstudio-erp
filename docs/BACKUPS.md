# Copias de seguridad

Los datos contables (facturas, asientos, caja, banco, stock, socios, usuarios y auditoría) viven en
PostgreSQL, dentro del volumen de Docker `postgres_data`. Ese volumen **se borra** con
`docker compose down -v`, y se pierde con la PC. Por eso hay scripts propios.

La pestaña *Configuración > Copia de seguridad* solo guarda los datos de la empresa (razón social,
CUIT, alícuota): esa configuración está en el navegador, no en la base.

## Hacer una copia

Desde la carpeta del proyecto, con el sistema levantado (`docker compose up`), en PowerShell:

```powershell
.\scripts\backup.ps1
```

Crea `backups\gymstudio_AAAAMMDD_HHMMSS.dump` y su huella `.sha256`. No hace falta instalar nada: usa
el `pg_dump` del contenedor de la base. Antes de guardar la copia verifica que se pueda leer y se
conservan las últimas 14 (se cambia con `-Conservar 30`; `-Carpeta D:\Respaldos` la guarda en otro lugar).

## Hacerla automática

```powershell
.\scripts\programar-backup.ps1            # todos los días a las 22:00
.\scripts\programar-backup.ps1 -Hora 18:30
.\scripts\programar-backup.ps1 -Quitar    # la desactiva
```

Usa el Programador de tareas de Windows; si la PC estaba apagada a esa hora, la copia se hace apenas
se prende. Requiere Docker Desktop abierto.

## Restaurar

```powershell
.\scripts\restaurar.ps1 -Archivo .\backups\gymstudio_20261027_142741.dump            # solo avisa qué haría
.\scripts\restaurar.ps1 -Archivo .\backups\gymstudio_20261027_142741.dump -Confirmar # restaura
```

Reemplaza **todos** los datos actuales por los de la copia. Antes de tocar nada:

1. verifica la huella SHA-256 del archivo (si está dañado, se detiene);
2. comprueba que sea una copia válida de PostgreSQL;
3. hace una copia de lo que hay ahora, `antes-de-restaurar_*.dump`, por si te arrepentís.

Al terminar reinicia el backend.

## Guardarla fuera de la PC

Una copia en la misma PC se pierde con ella. Copiá la carpeta `backups` a un pendrive o a la nube
(Drive, OneDrive) de forma periódica. Las copias traen datos reales, incluidos los hashes de contraseñas
de los usuarios: guardalas en un lugar privado.

## Cómo se probó

Contra una copia local con el ejercicio de la cátedra (30 facturas):

- se hizo una copia, se cargó una factura, un cliente y un usuario, y se restauró;
- las 21 tablas quedaron con exactamente los mismos registros que antes de alterarlos;
- siguieron funcionando los triggers y restricciones (13 de 13 pruebas de `pruebas_base_de_datos.sql`),
  el inicio de sesión, el saldo de Caja y la emisión de facturas;
- una copia con un byte alterado, un archivo que no es una copia y un archivo inexistente se rechazan
  sin tocar la base.
