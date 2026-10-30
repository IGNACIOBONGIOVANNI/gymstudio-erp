<#
.SYNOPSIS
  Compara una copia de seguridad con el sistema actual, sin modificar nada.

.DESCRIPTION
  Verifica que el archivo esté sano (huella SHA-256), compara la versión del sistema y de la base con las de
  ahora y dice, tabla por tabla, qué cambió desde que se hizo la copia. Sirve para saber si una copia está al
  día, si conviene hacer otra, o si es de una versión que no se puede restaurar tal cual.

  Códigos de salida: 0 = la copia es idéntica al sistema actual, 2 = es de un estado o versión anterior,
  1 = el archivo está dañado o hubo un error.

.EXAMPLE
  .\scripts\comparar-backup.ps1 -Archivo .\backups\gymstudio_20261029_233000.dump
#>
param(
    [Parameter(Mandatory)][string]$Archivo,
    [string]$Contenedor,
    [string]$BaseDeDatos = 'estudio_contable',
    [string]$Usuario = 'postgres'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_comun.ps1')
function Falla($mensaje) { Write-Host "ERROR: $mensaje" -ForegroundColor Red; exit 1 }

if (-not (Test-Path $Archivo)) { Falla "No existe el archivo: $Archivo" }
$Archivo = (Resolve-Path $Archivo).Path

if (-not $Contenedor) {
    $compose = Join-Path $PSScriptRoot '..\docker-compose.yml'
    $Contenedor = (docker compose -f $compose ps -q db 2>$null | Select-Object -First 1)
    if (-not $Contenedor) { Falla 'No encontré el contenedor de la base de datos. Abrí Docker Desktop y levantá el sistema con "docker compose up".' }
}

try { $resultado = Comparar-ConElSistema -Archivo $Archivo -Contenedor $Contenedor -BaseDeDatos $BaseDeDatos -Usuario $Usuario }
catch { Falla $_.Exception.Message }

switch ($resultado.Estado) {
    'identica'       { exit 0 }
    'danada'         { exit 1 }
    'sin-manifiesto' { exit 2 }
    default          { exit 2 }
}
