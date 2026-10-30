<#
  Funciones que comparten backup.ps1, comparar-backup.ps1 y restaurar.ps1.
  No se ejecuta solo: los otros scripts lo cargan con ". .\_comun.ps1".
#>

# Por cada tabla: cantidad de filas y una huella (MD5) de todo su contenido, ordenado. Sirve para saber si
# los datos de dos momentos son idénticos sin comparar fila por fila. Sin tildes: viaja por la entrada de psql.
$script:SqlHuella = @'
SELECT t.table_schema || '.' || t.table_name AS tabla,
       (xpath('/row/n/text()', query_to_xml(format('SELECT count(*) AS n FROM %I.%I', t.table_schema, t.table_name), false, true, '')))[1]::text AS filas,
       (xpath('/row/h/text()', query_to_xml(format('SELECT md5(coalesce(string_agg(x::text, ''|'' ORDER BY x::text), '''')) AS h FROM %I.%I x', t.table_schema, t.table_name), false, true, '')))[1]::text AS huella
FROM information_schema.tables t
WHERE t.table_type = 'BASE TABLE' AND t.table_schema NOT IN ('pg_catalog', 'information_schema')
ORDER BY 1;
'@

$script:SqlMigracion = @'
SELECT coalesce(max("MigrationId"), '') || '|' || count(*) FROM public."__EFMigrationsHistory";
'@

function Obtener-Sha256DeTexto {
    param([string]$Texto)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return (($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Texto)) | ForEach-Object { $_.ToString('x2') }) -join '') }
    finally { $sha.Dispose() }
}

<#
  Estado de los datos de la base en este momento: migración aplicada, cantidad de filas y huella de cada tabla,
  y una huella general de todo junto.
#>
function Obtener-HuellaDeDatos {
    param([string]$Contenedor, [string]$BaseDeDatos, [string]$Usuario)

    $filas = $script:SqlHuella | docker exec -i $Contenedor psql -U $Usuario -d $BaseDeDatos -At -F '|' -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw "No se pudo leer el estado de la base '$BaseDeDatos'. ¿Está levantado el sistema?" }

    $tablas = [ordered]@{}
    foreach ($linea in $filas) {
        $partes = "$linea" -split '\|'
        if ($partes.Count -eq 3) { $tablas[$partes[0]] = [ordered]@{ filas = [int64]$partes[1]; huella = $partes[2] } }
    }
    if ($tablas.Count -eq 0) { throw "La base '$BaseDeDatos' no tiene tablas." }

    $migracion = ($script:SqlMigracion | docker exec -i $Contenedor psql -U $Usuario -d $BaseDeDatos -At -v ON_ERROR_STOP=1 | Select-Object -First 1)
    $partesMigracion = "$migracion" -split '\|'

    $texto = ($tablas.GetEnumerator() | ForEach-Object { "$($_.Key)|$($_.Value.filas)|$($_.Value.huella)" }) -join "`n"
    [pscustomobject]@{
        Tablas      = $tablas
        Migracion   = $partesMigracion[0]
        Migraciones = [int]$partesMigracion[1]
        General     = Obtener-Sha256DeTexto $texto
    }
}

<# Migraciones que trae el código del sistema (las que la API aplicará al arrancar). #>
function Obtener-MigracionesDelCodigo {
    $carpeta = Join-Path $PSScriptRoot '..\backend\src\EstudioContable.Infrastructure\Migrations'
    if (-not (Test-Path $carpeta)) { return @() }
    @(Get-ChildItem $carpeta -Filter '*.cs' |
        Where-Object { $_.Name -match '^\d{14}_.+\.cs$' -and $_.Name -notmatch '\.Designer\.cs$' } |
        ForEach-Object { $_.BaseName } | Sort-Object)
}

<# Versión del sistema que muestra "Acerca de" (frontend\src\acercaDe.ts). #>
function Obtener-VersionDelSistema {
    $archivo = Join-Path $PSScriptRoot '..\frontend\src\acercaDe.ts'
    if (Test-Path $archivo) {
        $linea = Select-String -Path $archivo -Pattern "export const VERSION\s*=\s*'([^']+)'" | Select-Object -First 1
        if ($linea) { return $linea.Matches[0].Groups[1].Value }
    }
    return 'desconocida'
}

function Leer-Manifiesto {
    param([string]$Archivo)
    $ruta = "$Archivo.manifiesto.json"
    if (Test-Path $ruta) { return (Get-Content $ruta -Raw -Encoding UTF8 | ConvertFrom-Json) }
    return $null
}

<#
  Diferencias entre las tablas de una copia (manifiesto) y las del sistema actual.
  Devuelve una lista de objetos: Tabla, Detalle.
#>
function Comparar-Tablas {
    param($Manifiesto, $Actual)

    $diferencias = @()
    $enCopia = @{}
    foreach ($p in $Manifiesto.tablas.PSObject.Properties) { $enCopia[$p.Name] = $p.Value }

    foreach ($nombre in $enCopia.Keys | Sort-Object) {
        $c = $enCopia[$nombre]
        if (-not $Actual.Tablas.Contains($nombre)) {
            $diferencias += [pscustomobject]@{ Tabla = $nombre; Detalle = "está en la copia ($($c.filas) filas) y ya no existe en el sistema" }
            continue
        }
        $a = $Actual.Tablas[$nombre]
        if ($c.filas -ne $a.filas) {
            $diferencias += [pscustomobject]@{ Tabla = $nombre; Detalle = "la copia tiene $($c.filas) filas y ahora hay $($a.filas)" }
        } elseif ($c.huella -ne $a.huella) {
            $diferencias += [pscustomobject]@{ Tabla = $nombre; Detalle = "mismas $($c.filas) filas, pero el contenido cambió" }
        }
    }
    foreach ($nombre in $Actual.Tablas.Keys) {
        if (-not $enCopia.ContainsKey($nombre)) {
            $diferencias += [pscustomobject]@{ Tabla = $nombre; Detalle = "tabla nueva en el sistema ($($Actual.Tablas[$nombre].filas) filas), no estaba en la copia" }
        }
    }
    return $diferencias
}

function Escribir-Linea {
    param([string]$Marca, [string]$Texto)
    $color = switch ($Marca) { 'OK' { 'Green' } 'AVISO' { 'Yellow' } 'ERROR' { 'Red' } default { 'Gray' } }
    Write-Host ('[{0,-5}] {1}' -f $Marca, $Texto) -ForegroundColor $color
}

<#
  Compara una copia de seguridad con el sistema actual y lo cuenta en pantalla:
    1) integridad: la huella SHA-256 del archivo es la que se guardó al hacerlo;
    2) versión: la del sistema cuando se hizo la copia contra la de ahora, y la migración de la base
       contra la que trae el código;
    3) datos: tabla por tabla, qué cambió desde la copia.
  Devuelve un objeto con Estado: identica | anterior | otra-version | danada | sin-manifiesto | sin-sistema.
#>
function Comparar-ConElSistema {
    param([string]$Archivo, [string]$Contenedor, [string]$BaseDeDatos, [string]$Usuario)

    $nombre = Split-Path $Archivo -Leaf
    $mb = [math]::Round((Get-Item $Archivo).Length / 1MB, 2)
    $manifiesto = Leer-Manifiesto $Archivo
    $real = (Get-FileHash $Archivo -Algorithm SHA256).Hash

    $resultado = [pscustomobject]@{
        Estado = 'sin-manifiesto'; Integra = $null; CopiaMasNuevaQueElCodigo = $false
        Diferencias = @(); MismaMigracion = $null
    }

    Write-Host ''
    if ($manifiesto) {
        Write-Host ("Copia: {0} ({1} MB), hecha el {2:dd/MM/yyyy HH:mm}" -f $nombre, $mb, [datetime]$manifiesto.fecha)
    } else {
        Write-Host ("Copia: {0} ({1} MB)" -f $nombre, $mb)
    }

    # 1) Integridad: contra el manifiesto y contra el archivo .sha256 de al lado.
    $esperadas = @()
    if ($manifiesto) { $esperadas += $manifiesto.sha256 }
    if (Test-Path "$Archivo.sha256") { $esperadas += ((Get-Content "$Archivo.sha256" -TotalCount 1) -split '\s+')[0] }
    if ($esperadas.Count -eq 0) {
        Escribir-Linea 'AVISO' 'Integridad: no hay huella guardada junto a la copia, no se puede verificar el archivo.'
    } elseif ($esperadas | Where-Object { $_ -ne $real }) {
        Escribir-Linea 'ERROR' 'Integridad: la huella SHA-256 NO coincide. El archivo está dañado o fue modificado.'
        $resultado.Integra = $false
        $resultado.Estado = 'danada'
        return $resultado
    } else {
        Escribir-Linea 'OK' "Integridad: la huella SHA-256 del archivo coincide con la guardada ($($real.Substring(0, 12))…)."
        $resultado.Integra = $true
    }

    if (-not $manifiesto) {
        Escribir-Linea 'AVISO' 'Esta copia es anterior al manifiesto: no trae versión ni huella de datos, así que no se puede comparar con el sistema actual.'
        return $resultado
    }

    # Si la base actual no se puede leer (por ejemplo, una PC nueva donde todavía está vacía), se compara solo con el código.
    $actual = $null
    try { $actual = Obtener-HuellaDeDatos -Contenedor $Contenedor -BaseDeDatos $BaseDeDatos -Usuario $Usuario }
    catch { Escribir-Linea 'AVISO' "No se pudo leer el estado actual de la base ($($_.Exception.Message)). Se comprueba la copia solo contra el código instalado." }

    # 2) Versión del sistema y de la base.
    $versionActual = Obtener-VersionDelSistema
    if ($manifiesto.version -eq $versionActual) {
        Escribir-Linea 'OK' "Versión del sistema: $($manifiesto.version) (la misma de ahora)."
    } else {
        Escribir-Linea 'AVISO' "Versión del sistema: la copia es de la $($manifiesto.version) y ahora es la $versionActual."
    }

    $migracionesCodigo = Obtener-MigracionesDelCodigo
    if ($actual) {
        $resultado.MismaMigracion = ($manifiesto.migracion -eq $actual.Migracion)
        if ($resultado.MismaMigracion) {
            Escribir-Linea 'OK' "Esquema de la base: misma migración que ahora ($($actual.Migracion), $($actual.Migraciones) en total)."
        } else {
            Escribir-Linea 'AVISO' "Esquema de la base: la copia está en la migración $($manifiesto.migracion) ($($manifiesto.migraciones) en total) y el sistema en la $($actual.Migracion) ($($actual.Migraciones))."
        }
    }
    if ($migracionesCodigo.Count -gt 0 -and $manifiesto.migracion -and ($migracionesCodigo -notcontains $manifiesto.migracion)) {
        $resultado.CopiaMasNuevaQueElCodigo = $true
        Escribir-Linea 'ERROR' "La copia usa la migración $($manifiesto.migracion), que este código no conoce: es de una versión MÁS NUEVA del sistema."
    } elseif ($migracionesCodigo.Count -gt 0 -and $manifiesto.migraciones -lt $migracionesCodigo.Count) {
        $faltan = $migracionesCodigo.Count - $manifiesto.migraciones
        Escribir-Linea 'AVISO' "La copia es de una versión anterior: al restaurarla, el sistema aplicará $faltan migración(es) al arrancar."
    }

    # 3) Datos.
    if (-not $actual) {
        $resultado.Estado = if ($resultado.CopiaMasNuevaQueElCodigo) { 'otra-version' } else { 'sin-sistema' }
        return $resultado
    }
    $resultado.Diferencias = @(Comparar-Tablas $manifiesto $actual)
    $totalTablas = $actual.Tablas.Count
    if ($resultado.Diferencias.Count -eq 0) {
        Escribir-Linea 'OK' "Datos: las $totalTablas tablas son idénticas a las del sistema actual."
    } else {
        Escribir-Linea 'AVISO' "Datos: cambiaron $($resultado.Diferencias.Count) de $totalTablas tablas desde que se hizo la copia:"
        foreach ($d in ($resultado.Diferencias | Select-Object -First 15)) { Write-Host ("          {0}: {1}" -f $d.Tabla, $d.Detalle) }
        if ($resultado.Diferencias.Count -gt 15) { Write-Host "          ... y $($resultado.Diferencias.Count - 15) más." }
    }

    if ($resultado.CopiaMasNuevaQueElCodigo -or -not $resultado.MismaMigracion) { $resultado.Estado = 'otra-version' }
    elseif ($resultado.Diferencias.Count -eq 0) { $resultado.Estado = 'identica' }
    else { $resultado.Estado = 'anterior' }

    Write-Host ''
    switch ($resultado.Estado) {
        'identica'     { Write-Host 'Resultado: la copia es IDÉNTICA al sistema actual.' -ForegroundColor Green }
        'anterior'     { Write-Host 'Resultado: la copia es de un estado anterior del sistema (se cargaron o cambiaron datos después).' -ForegroundColor Yellow }
        'otra-version' { Write-Host 'Resultado: la copia es de OTRA versión del esquema de la base que la actual.' -ForegroundColor Yellow }
    }
    return $resultado
}
