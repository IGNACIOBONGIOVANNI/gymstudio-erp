<#
.SYNOPSIS
  Programa una copia de seguridad automática todos los días (Programador de tareas de Windows).

.EXAMPLE
  .\scripts\programar-backup.ps1                 # todos los días a las 22:00
  .\scripts\programar-backup.ps1 -Hora 18:30
  .\scripts\programar-backup.ps1 -Quitar         # elimina la tarea programada
#>
param(
    [string]$Hora = '22:00',
    [switch]$Quitar
)

$ErrorActionPreference = 'Stop'
$nombreTarea = 'GymStudio - copia de seguridad diaria'

if ($Quitar) {
    Unregister-ScheduledTask -TaskName $nombreTarea -Confirm:$false -ErrorAction SilentlyContinue
    Write-Host 'Tarea eliminada.' -ForegroundColor Green
    exit 0
}

$script = (Resolve-Path (Join-Path $PSScriptRoot 'backup.ps1')).Path
$accion = New-ScheduledTaskAction -Execute 'powershell.exe' `
    -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$script`"" -WorkingDirectory (Split-Path $script -Parent)
$disparador = New-ScheduledTaskTrigger -Daily -At $Hora
# Si la PC estaba apagada a esa hora, la copia se hace apenas se prende.
$ajustes = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries

Register-ScheduledTask -TaskName $nombreTarea -Action $accion -Trigger $disparador -Settings $ajustes `
    -Description 'Copia de la base de datos de GymStudio ERP (scripts\backup.ps1)' -Force | Out-Null
Write-Host "Listo: copia automática todos los días a las $Hora." -ForegroundColor Green
Write-Host 'Las copias quedan en la carpeta "backups" del proyecto (se conservan las últimas 14).'
Write-Host 'Requiere que Docker Desktop esté abierto a esa hora.'
