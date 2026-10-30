<#
.SYNOPSIS
  Restaura la base de datos desde una copia hecha con backup.ps1.

.DESCRIPTION
  REEMPLAZA todos los datos actuales por los de la copia. Antes de tocar nada verifica la huella del
  archivo, lo compara con el sistema actual (versión, esquema y datos que cambiarían), hace una copia
  de seguridad de lo que hay ("antes-de-restaurar") y, si el sistema corre con docker-compose, reinicia
  el backend al terminar. Una copia de una versión MÁS NUEVA que el código no se restaura (con -Forzar sí).

.EXAMPLE
  .\scripts\restaurar.ps1 -Archivo .\backups\gymstudio_20261027_120000.dump -Confirmar
#>
param(
    [Parameter(Mandatory)][string]$Archivo,
    [switch]$Confirmar,                     # sin este parámetro sólo muestra qué haría
    [switch]$Forzar,                        # restaura aunque la copia sea de una versión más nueva que el código
    [string]$Contenedor,
    [string]$BaseDeDatos = 'estudio_contable',
    [string]$Usuario = 'postgres'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_comun.ps1')
function Falla($mensaje) { Write-Host "ERROR: $mensaje" -ForegroundColor Red; exit 1 }

if (-not (Test-Path $Archivo)) { Falla "No existe el archivo: $Archivo" }
$Archivo = (Resolve-Path $Archivo).Path
$nombre = Split-Path $Archivo -Leaf

$enCompose = -not $Contenedor
$compose = Join-Path $PSScriptRoot '..\docker-compose.yml'
if (-not $Contenedor) {
    $Contenedor = (docker compose -f $compose ps -q db 2>$null | Select-Object -First 1)
    if (-not $Contenedor) { Falla 'No encontré el contenedor de la base de datos. Levantá el sistema con "docker compose up".' }
}

# 1) El archivo tiene que ser el mismo que se guardó, y se compara con el sistema actual: qué versión es,
#    qué esquema trae y qué datos cambiarían al restaurarla.
try { $comparacion = Comparar-ConElSistema -Archivo $Archivo -Contenedor $Contenedor -BaseDeDatos $BaseDeDatos -Usuario $Usuario }
catch { Falla $_.Exception.Message }
if ($comparacion.Estado -eq 'danada') { Falla 'El archivo está dañado o fue modificado (la huella SHA-256 no coincide). No se restauró nada.' }
if ($comparacion.CopiaMasNuevaQueElCodigo -and -not $Forzar) {
    Falla 'La copia es de una versión MÁS NUEVA del sistema que el código que está instalado: restaurarla dejaría una base que este código no entiende. Actualizá el sistema primero (o repetí con -Forzar si sabés lo que hacés). No se restauró nada.'
}

if (-not $Confirmar) {
    Write-Host ''
    Write-Host "Esto REEMPLAZARÍA todos los datos de '$BaseDeDatos' por los de $nombre." -ForegroundColor Yellow
    Write-Host 'Para hacerlo, repetí el comando agregando -Confirmar'
    exit 0
}

# 2) El archivo tiene que ser una copia válida de PostgreSQL (se comprueba antes de tocar nada).
$temporal = "/tmp/$nombre"
docker cp $Archivo "${Contenedor}:$temporal" | Out-Null
docker exec $Contenedor pg_restore --list $temporal | Out-Null
if ($LASTEXITCODE -ne 0) {
    docker exec $Contenedor rm -f $temporal | Out-Null
    Falla 'El archivo no es una copia válida de PostgreSQL. No se restauró nada.'
}

# 3) Red de seguridad: copia de lo que hay ahora.
Write-Host 'Copia de seguridad de los datos actuales...'
$carpeta = Split-Path $Archivo -Parent
& (Join-Path $PSScriptRoot 'backup.ps1') -Carpeta $carpeta -Prefijo 'antes-de-restaurar' -Conservar 5 `
    -Contenedor $Contenedor -BaseDeDatos $BaseDeDatos -Usuario $Usuario
if ($LASTEXITCODE -ne 0) { docker exec $Contenedor rm -f $temporal | Out-Null; Falla 'No se pudo hacer la copia previa. No se restauró nada.' }

# 4) Se recrea la base y se carga la copia. Con FORCE se cierran las conexiones del backend.

Write-Host "Restaurando $nombre en '$BaseDeDatos'..."
docker exec $Contenedor psql -U $Usuario -d postgres -v ON_ERROR_STOP=1 -q -c "DROP DATABASE IF EXISTS `"$BaseDeDatos`" WITH (FORCE)" -c "CREATE DATABASE `"$BaseDeDatos`""
if ($LASTEXITCODE -ne 0) { Falla 'No se pudo recrear la base.' }

docker exec $Contenedor pg_restore -U $Usuario -d $BaseDeDatos --no-owner --exit-on-error $temporal
$codigo = $LASTEXITCODE
docker exec $Contenedor rm -f $temporal | Out-Null
if ($codigo -ne 0) {
    Falla "La restauración falló. Tus datos anteriores están en la copia 'antes-de-restaurar_*.dump' de la carpeta $carpeta."
}

if ($enCompose) {
    Write-Host 'Reiniciando el backend...'
    docker compose -f $compose restart backend | Out-Null
}
Write-Host "Listo: base restaurada desde $nombre." -ForegroundColor Green
Write-Host "Los datos que había antes quedaron en 'antes-de-restaurar_*.dump' (carpeta $carpeta)."
