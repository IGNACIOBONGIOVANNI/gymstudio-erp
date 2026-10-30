<#
.SYNOPSIS
  Copia de seguridad de TODA la base de datos (facturas, asientos, caja, banco, stock,
  socios, usuarios y auditoría) en la carpeta "backups".

.DESCRIPTION
  Usa el pg_dump del contenedor de PostgreSQL, así que no hay que instalar nada en la PC.
  Cada copia se verifica (se lee el índice del archivo) y se guarda su huella SHA-256 para
  detectar archivos dañados. Junto a la copia queda un manifiesto (.manifiesto.json) con la versión
  del sistema, la migración de la base y una huella de los datos de cada tabla: con
  comparar-backup.ps1 se ve después si la copia es idéntica al sistema actual o qué cambió.
  Se conservan las últimas N copias.

.EXAMPLE
  .\scripts\backup.ps1
  .\scripts\backup.ps1 -Conservar 30 -Carpeta D:\Respaldos\GymStudio
#>
param(
    [string]$Carpeta = (Join-Path $PSScriptRoot '..\backups'),
    [int]$Conservar = 14,
    [string]$Prefijo = 'gymstudio',
    [string]$Contenedor,                    # por defecto: el servicio "db" de docker-compose
    [string]$BaseDeDatos = 'estudio_contable',
    [string]$Usuario = 'postgres'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_comun.ps1')

function Falla($mensaje) { Write-Host "ERROR: $mensaje" -ForegroundColor Red; exit 1 }

if (-not $Contenedor) {
    $compose = Join-Path $PSScriptRoot '..\docker-compose.yml'
    $Contenedor = (docker compose -f $compose ps -q db 2>$null | Select-Object -First 1)
    if (-not $Contenedor) { Falla 'No encontré el contenedor de la base de datos. Abrí Docker Desktop y levantá el sistema con "docker compose up".' }
}

New-Item -ItemType Directory -Force -Path $Carpeta | Out-Null
$Carpeta = (Resolve-Path $Carpeta).Path
$marca = Get-Date -Format 'yyyyMMdd_HHmmss'
$nombre = "${Prefijo}_$marca.dump"
$destino = Join-Path $Carpeta $nombre
$temporal = "/tmp/$nombre"

# 1) Se genera dentro del contenedor (formato personalizado: comprimido y restaurable por partes).
#    No se redirige la salida por PowerShell porque dañaría el archivo binario.
#    La huella de los datos se toma antes y después: si alguien cargó algo mientras se copiaba, se repite,
#    para que la huella del manifiesto sea exactamente la de lo que quedó en el archivo.
$huella = $null
$consistente = $false
for ($intento = 1; $intento -le 3 -and -not $consistente; $intento++) {
    try { $antes = Obtener-HuellaDeDatos -Contenedor $Contenedor -BaseDeDatos $BaseDeDatos -Usuario $Usuario }
    catch { Falla $_.Exception.Message }

    docker exec $Contenedor pg_dump -U $Usuario -d $BaseDeDatos -Fc -Z 6 -f $temporal
    if ($LASTEXITCODE -ne 0) { Falla "pg_dump falló (base '$BaseDeDatos'). ¿Está levantado el sistema?" }

    $despues = Obtener-HuellaDeDatos -Contenedor $Contenedor -BaseDeDatos $BaseDeDatos -Usuario $Usuario
    $consistente = ($antes.General -eq $despues.General)
    $huella = $antes
    if (-not $consistente -and $intento -lt 3) { Write-Host 'Se modificaron datos mientras se hacía la copia; la repito...' -ForegroundColor Yellow }
}

# 2) Se verifica antes de copiarlo: el índice del archivo tiene que poder leerse.
$indice = docker exec $Contenedor sh -c "pg_restore --list $temporal | grep -c 'TABLE DATA'"
if ($LASTEXITCODE -ne 0 -or [int]$indice -lt 5) {
    docker exec $Contenedor rm -f $temporal | Out-Null
    Falla "La copia salió incompleta (tablas con datos: $indice). No se guardó."
}

docker cp "${Contenedor}:$temporal" $destino | Out-Null
docker exec $Contenedor rm -f $temporal | Out-Null
if (-not (Test-Path $destino) -or (Get-Item $destino).Length -lt 1024) { Falla 'No se pudo copiar el archivo al disco.' }

# 3) Huella para detectar daños o cambios del archivo, y manifiesto con el estado del sistema.
$sha = (Get-FileHash $destino -Algorithm SHA256).Hash
"$sha  $nombre" | Set-Content -Encoding ASCII "$destino.sha256"

$manifiesto = [ordered]@{
    archivo       = $nombre
    sha256        = $sha
    bytes         = (Get-Item $destino).Length
    fecha         = (Get-Date).ToString('o')
    version       = (Obtener-VersionDelSistema)
    baseDeDatos   = $BaseDeDatos
    migracion     = $huella.Migracion
    migraciones   = $huella.Migraciones
    consistente   = $consistente
    huellaGeneral = $huella.General
    tablas        = $huella.Tablas
}
$json = $manifiesto | ConvertTo-Json -Depth 6
[System.IO.File]::WriteAllText("$destino.manifiesto.json", $json, (New-Object System.Text.UTF8Encoding($false)))

# 4) Rotación: se conservan las últimas N copias de este prefijo.
$viejas = Get-ChildItem $Carpeta -Filter "${Prefijo}_*.dump" | Sort-Object Name -Descending | Select-Object -Skip $Conservar
foreach ($v in $viejas) { Remove-Item $v.FullName, "$($v.FullName).sha256", "$($v.FullName).manifiesto.json" -Force -ErrorAction SilentlyContinue }

$mb = [math]::Round((Get-Item $destino).Length / 1MB, 2)
$filas = ($huella.Tablas.Values | ForEach-Object { $_['filas'] } | Measure-Object -Sum).Sum
Write-Host "Copia lista: $destino ($mb MB, $indice tablas con datos)" -ForegroundColor Green
Write-Host "Huella SHA-256: $sha"
Write-Host "Versión del sistema: $($manifiesto.version). Base en la migración $($huella.Migracion) ($($huella.Migraciones) en total). Datos: $($huella.Tablas.Count) tablas, $filas filas."
if (-not $consistente) {
    Write-Host 'Aviso: los datos seguían cambiando durante la copia; la huella de datos del manifiesto puede no coincidir con el archivo. Repetí la copia cuando nadie esté cargando datos.' -ForegroundColor Yellow
}
Write-Host "Se conservan las últimas $Conservar copias en $Carpeta."
Write-Host 'Para ver si esta copia coincide con el sistema actual: .\scripts\comparar-backup.ps1 -Archivo <la copia>'
Write-Host "Importante: copiá esta carpeta también a un pendrive o a la nube. Si se rompe la PC, las copias que están en la misma PC se pierden con ella." -ForegroundColor Yellow
