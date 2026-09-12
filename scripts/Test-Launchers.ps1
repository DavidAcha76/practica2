param([switch]$WithApis)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $root ('.logs/bat review ' + [guid]::NewGuid().ToString('N'))
$fixtureScripts = Join-Path $fixture 'scripts'
$caller = Join-Path $fixture 'carpeta externa'
New-Item -ItemType Directory -Force -Path $fixtureScripts,$caller | Out-Null
$runnerSource = Get-Content (Join-Path $PSScriptRoot 'Run-Stack.ps1') -Raw -Encoding UTF8
$eventsPath = Join-Path $fixture 'events.txt'
$script:passed = 0

function Assert($Condition, [string]$Message) {
    if (-not $Condition) { throw "FALLO: $Message. Evidencia: $fixture" }
    $script:passed++
    Write-Host "OK: $Message"
}

# La copia ejecuta los BAT y el coordinador reales. Solo sustituye operaciones
# externas: Docker, compilaciones, procesos HTTP y Bootstrap. No toca bases.
$fakes = @'
function Event([string]$Text) { Add-Content (Join-Path $repositoryRoot 'events.txt') $Text }
function Ensure-Docker { Event 'docker-ready' }
function Invoke-Compose([string[]]$Arguments) { Event ('compose ' + ($Arguments -join ' ')) }
function Build-Project([string]$RelativePath, [string]$LogName) {
    Event ('build ' + $RelativePath)
    if ($env:PRACTICA_TEST_FAILURE -eq 'build' -and $RelativePath -like '*BCB*') { throw 'Compilacion simulada fallida' }
}
function Invoke-Bootstrap([string]$Command, [string]$InputCsv = '') {
    Event ('bootstrap ' + $Command)
    if ($env:PRACTICA_TEST_FAILURE -eq $Command) { throw ('Fallo simulado de ' + $Command) }
    if ($Command -eq 'import') { [IO.File]::WriteAllText((Join-Path $repositoryRoot 'csv-path.txt'), $InputCsv) }
}
function Get-OwnedProcess($Service) { return [pscustomobject]@{ Id = $Service.ProcessId } }
function Stop-OwnedServices {
    Event 'stop-services'
    $script:services = @()
    Save-State
}
function Start-Api([string]$RelativeDirectory, [string]$Name, [string]$Url, [string]$HealthPath, [int]$BankId = 0) {
    Event ('start ' + $Name)
    $script:services += [pscustomobject]@{ Name=$Name; ProcessId=1; BankId=$BankId; HealthUrl=$Url+$HealthPath }
    Save-State
}
function Wait-Api($Service) { Event ('health ' + $Service.Name) }
function Invoke-RestMethod {
    [pscustomobject]@{ type='BCB'; name='BCB'; url='simulado'; ok=$true }
    $count = if ($env:PRACTICA_TEST_FAILURE -eq 'bank-count') { 13 } else { 14 }
    1..$count | ForEach-Object { [pscustomobject]@{ type='Banco'; name="Banco $_"; url='simulado'; ok=($env:PRACTICA_TEST_FAILURE -ne 'http') } }
    [pscustomobject]@{ type='Worker'; name='Worker remoto'; url='simulado'; ok=$false }
}
'@
Assert ($runnerSource.Contains('$lock = $null')) 'Punto de aislamiento encontrado'
$isolatedRunner = $runnerSource.Replace('$lock = $null', $fakes + "`r`n" + '$lock = $null')
[IO.File]::WriteAllText((Join-Path $fixtureScripts 'Run-Stack.ps1'), $isolatedRunner)
foreach ($bat in @('INICIAR_TODO.bat','CARGAR_CSV.bat','VACIAR_BD.bat')) {
    Copy-Item -LiteralPath (Join-Path $root $bat) -Destination $fixture
}
foreach ($bank in Get-ChildItem (Join-Path $root '14_Bancos_APIs_Cifradas/src') -Directory -Filter Banco*.Api) {
    $destination = Join-Path $fixture ('14_Bancos_APIs_Cifradas/src/' + $bank.Name)
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Copy-Item -LiteralPath (Join-Path $bank.FullName 'appsettings.json') -Destination $destination
}
$bootstrapDirectory = Join-Path $fixture 'BD/Bootstrap/bin/Debug/net10.0'
New-Item -ItemType Directory -Force -Path $bootstrapDirectory | Out-Null
Set-Content (Join-Path $bootstrapDirectory 'Bootstrap.dll') 'Placeholder: nunca se ejecuta'
Set-Content (Join-Path $fixture 'BD/dataset.csv') 'CSV simulado'
Set-Content (Join-Path $caller 'mi prueba.csv') 'CSV simulado'

function Run-Bat([string]$Name, [string]$Argument = '', [int]$ExpectedExit = 0, [string]$Failure = '') {
    Set-Content -LiteralPath $eventsPath -Value ''
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = $env:ComSpec
    $start.Arguments = '/d /s /c ""' + (Join-Path $fixture $Name) + '" ' + $Argument + ' <nul"'
    $start.WorkingDirectory = $caller
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = 'Hidden'
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.EnvironmentVariables['PRACTICA_TEST_FAILURE'] = $Failure
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(45000)) { $process.Kill(); throw 'El BAT no termino en 45 segundos.' }
    $output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
    Add-Content (Join-Path $fixture 'bat-output.log') ($Name + "`r`n" + $output)
    Assert ($process.ExitCode -eq $ExpectedExit) "$Name devuelve $ExpectedExit (fallo simulado: $Failure)"
    $process.Dispose()
    $script:events = @(Get-Content -LiteralPath $eventsPath | Where-Object { $_ })
    $script:lastOutput = $output
}

Run-Bat 'INICIAR_TODO.bat'
Assert (@($events | Where-Object { $_ -like 'start *' }).Count -eq 16) 'Arranque: exactamente 14 bancos, BCB y ASFI'
Assert ($events.IndexOf('bootstrap configure') -lt $events.IndexOf('bootstrap init')) 'Se configura antes de inicializar'
Assert ($events -contains 'bootstrap check') 'Se comprueban las bases antes de declarar el arranque completo'
Assert ($events -notcontains 'bootstrap import' -and $events -notcontains 'bootstrap clear') 'Iniciar servicios conserva los datos y no carga el CSV'
Run-Bat 'INICIAR_TODO.bat'
Assert (@($events | Where-Object { $_ -like 'start *' }).Count -eq 0) 'Segundo arranque sin procesos duplicados'
Assert (($events -contains 'compose up -d --wait --wait-timeout 300') -and ($events -contains 'bootstrap check')) 'Segundo arranque recupera contenedores y revisa bases'
Run-Bat 'CARGAR_CSV.bat'
Assert ((Get-Content (Join-Path $fixture 'csv-path.txt') -Raw) -eq (Join-Path $fixture 'BD/dataset.csv')) 'CSV predeterminado correcto'
Run-Bat 'CARGAR_CSV.bat' '"mi prueba.csv"'
Assert ((Get-Content (Join-Path $fixture 'csv-path.txt') -Raw) -eq (Join-Path $caller 'mi prueba.csv')) 'CSV relativo con espacios desde otra carpeta'
Run-Bat 'VACIAR_BD.bat'
Assert ($events.IndexOf('stop-services') -lt $events.IndexOf('bootstrap clear')) 'Vaciado con APIs detenidas'
Assert ($events.IndexOf('build ASFI_Cluster_DotNet10/ASFI.Cluster.sln') -lt $events.IndexOf('bootstrap clear')) 'Todos los proyectos compilan antes del vaciado'
Assert (@($events | Where-Object { $_ -like 'start *' }).Count -eq 16) 'Se reinician los 16 servicios despues de vaciar'
Assert ($events.IndexOf('bootstrap clear') -lt $events.IndexOf('bootstrap configure')) 'Vaciado usa las conexiones actuales antes de reconfigurar'
Run-Bat 'CARGAR_CSV.bat'
Assert ($events -contains 'bootstrap import') 'Se puede volver a cargar despues del vaciado'
Run-Bat 'CARGAR_CSV.bat' '"no existe.csv"' 1
Assert ($events -notcontains 'bootstrap import') 'CSV inexistente rechazado antes de importar'
Run-Bat 'CARGAR_CSV.bat' '' 1 'http'
Assert ($events -notcontains 'bootstrap import') 'Importacion bloqueada si falla una API'
Run-Bat 'CARGAR_CSV.bat' '' 1 'bank-count'
Assert ($events -notcontains 'bootstrap import') 'Importacion bloqueada si ASFI omite un banco'
Run-Bat 'VACIAR_BD.bat' '' 1 'build'
Assert ($events -notcontains 'bootstrap clear') 'Error de compilacion impide borrar'
Run-Bat 'INICIAR_TODO.bat'
Run-Bat 'VACIAR_BD.bat' '' 1 'clear'
Assert (@($events | Where-Object { $_ -like 'start *' }).Count -eq 0) 'Vaciado fallido no inicia servicios ni informa exito'
Run-Bat 'CARGAR_CSV.bat' '' 1
Assert ($events -notcontains 'bootstrap import') 'Carga rechazada si el vaciado dejo los servicios detenidos'
Run-Bat 'INICIAR_TODO.bat'
Run-Bat 'INICIAR_TODO.bat' '' 1 'check'
Assert ($lastOutput -notlike '*Los servicios ya estaban iniciados*') 'Bases inaccesibles no se anuncian como listas'
$heldLock = [IO.File]::Open((Join-Path $fixture '.runtime/stack.lock'), 'Open', 'ReadWrite', 'None')
try {
    Run-Bat 'VACIAR_BD.bat' '' 1
    Assert ($events.Count -eq 0 -and $lastOutput -like '*en curso*') 'El bloqueo impide vaciados simultaneos antes de tocar servicios'
} finally { $heldLock.Dispose() }

# Ejecutar la funcion real de comandos nativos en PowerShell 5, con stderr y exit != 0.
$tokens = $null; $parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseInput($runnerSource, [ref]$tokens, [ref]$parseErrors)
Assert ($parseErrors.Count -eq 0) 'Run-Stack.ps1 sin errores de sintaxis'
$nativeFunction = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Invoke-Native' }, $false)
. ([scriptblock]::Create($nativeFunction.Extent.Text))
$nativeLog = Join-Path $fixture 'native.log'
$nativeErrorLog = Join-Path $fixture 'native.err.log'
$code = Invoke-Native $env:ComSpec @('/d','/c','echo linux & echo advertencia 1>&2 & exit /b 0') $nativeLog $nativeErrorLog
Assert ($code -eq 0 -and (Get-Content $nativeLog -Raw).Trim() -eq 'linux') 'stderr no rompe Docker ni contamina la deteccion de Linux'
$code = Invoke-Native $env:ComSpec @('/d','/c','echo error-simulado 1>&2 & exit /b 7') $nativeLog
Assert ($code -eq 7 -and $ErrorActionPreference -eq 'Stop') 'Se conserva el codigo de error y se restaura ErrorActionPreference'
if ($WithApis) {
    # APIs reales en puertos temporales, sin inicializar ni leer/escribir bases.
    # Los ejecutables deben estar compilados previamente.
    foreach ($name in @('Save-State','Get-OwnedProcess','Stop-OwnedServices','Start-Api','Wait-Api')) {
        $definition = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name }, $false)
        . ([scriptblock]::Create($definition.Extent.Text))
    }
    $repositoryRoot = $root
    $logDirectory = Join-Path $fixture 'apis-reales'
    New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
    $statePath = Join-Path $logDirectory 'processes.json'
    $script:services = @()
    $savedEnvironment = @{}
    function Set-TestEnvironment([string]$Name, [string]$Value) {
        if (-not $savedEnvironment.ContainsKey($Name)) { $savedEnvironment[$Name] = [Environment]::GetEnvironmentVariable($Name) }
        [Environment]::SetEnvironmentVariable($Name, $Value)
    }
    function Get-TestUrl {
        $listener = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, 0)
        try { $listener.Start(); return "http://127.0.0.1:$($listener.LocalEndpoint.Port)" }
        finally { $listener.Stop() }
    }
    try {
        Set-TestEnvironment 'BCB__ClaveAdministracion' ([guid]::NewGuid().ToString('N'))
        Set-TestEnvironment 'Database__InitializeOnStartup' 'false'
        Set-TestEnvironment 'Asfi__InitializeDatabaseOnStartup' 'false'
        Set-TestEnvironment 'Asfi__AuditDirectory' (Join-Path $logDirectory 'audit')
        Set-TestEnvironment 'Workers__0__Enabled' 'false'
        Set-TestEnvironment 'Workers__1__Enabled' 'false'
        $bcbUrl = Get-TestUrl
        Start-Api 'BCB_Cotizaciones_API/BCB.Cotizaciones.Api' 'BCB.Cotizaciones.Api' $bcbUrl '/api/cotizacion'
        Set-TestEnvironment 'BCB__BaseUrl' $bcbUrl
        foreach ($bank in Get-ChildItem (Join-Path $root '14_Bancos_APIs_Cifradas/src') -Directory -Filter Banco*.Api) {
            $config = Get-Content (Join-Path $bank.FullName 'appsettings.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            $url = Get-TestUrl
            Start-Api "14_Bancos_APIs_Cifradas/src/$($bank.Name)" $bank.Name $url '/api/banco/info' $config.Bank.BancoId
            Set-TestEnvironment "Banks__$($config.Bank.BancoId - 1)__BaseUrl" $url
        }
        $mainUrl = Get-TestUrl
        Start-Api 'ASFI_Cluster_DotNet10/Asfi.Main.Api' 'Asfi.Main.Api' $mainUrl '/api/asfi/config-summary'
        foreach ($service in $script:services) { Wait-Api $service }
        Assert ($script:services.Count -eq 16) 'Los 16 ejecutables reales arrancan y responden con la identidad esperada'
        $dependencies = Invoke-RestMethod "$mainUrl/api/asfi/dependencies" -TimeoutSec 15
        Assert ($dependencies.Count -eq 15 -and @($dependencies | Where-Object { -not $_.ok }).Count -eq 0) 'ASFI real conecta con los 14 bancos y BCB por HTTP'
        $saved = Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
        $script:services = @($saved)
        Assert (@($script:services | Where-Object { Get-OwnedProcess $_ }).Count -eq 16) 'Los 16 procesos reales se reconocen despues de recuperar los PID guardados'
    } finally {
        try { Stop-OwnedServices }
        finally { foreach ($key in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($key, $savedEnvironment[$key]) } }
    }
    Assert ($script:services.Count -eq 0) 'Los procesos de prueba quedan detenidos'
}
Write-Host "$script:passed comprobaciones OK. BAT reales con servicios simulados; sin acceso a bases. Logs: $fixture" -ForegroundColor Green
