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

Contra una copia local con el ejercicio de la cátedra (30 facturas):

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
