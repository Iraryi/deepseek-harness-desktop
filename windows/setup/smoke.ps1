param(
    [string]$FullSetup = "$PSScriptRoot\dist\DeepSeek-Harness-Setup-Full-0.1.0-rc.5-win-x64.exe",
    [string]$LiteSetup = "$PSScriptRoot\dist\DeepSeek-Harness-Setup-Lite-0.1.0-rc.5-win-x64.exe",
    [string]$RuntimeArchive = "$PSScriptRoot\..\runtime\dist\DeepSeek-Harness-Runtime-win-x64.zip"
)

$ErrorActionPreference = 'Stop'
$dist = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'dist'))
$full = [IO.Path]::GetFullPath($FullSetup)
$lite = [IO.Path]::GetFullPath($LiteSetup)
$runtime = [IO.Path]::GetFullPath($RuntimeArchive)
foreach ($path in @($full, $lite, $runtime)) {
    if (-not (Test-Path $path)) { throw "Setup smoke input is missing: $path" }
}

$testRoot = Join-Path $dist ('smoke-install-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$testRootPath = [IO.Path]::GetFullPath($testRoot)
if (-not $testRootPath.StartsWith($dist + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Setup smoke root must stay under $dist"
}
New-Item -ItemType Directory -Path $testRootPath | Out-Null

$previousLocalAppData = $env:LOCALAPPDATA
$fullApp = Join-Path $testRootPath '中文 Full 安装'
$fullLocal = Join-Path $testRootPath 'full-localappdata'
$liteApp = Join-Path $testRootPath 'English Lite Install'
$liteLocal = Join-Path $testRootPath 'lite-localappdata'
$windowControl = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) '..\work\winctl.exe'
$windowControl = [IO.Path]::GetFullPath($windowControl)

function Invoke-Setup([string]$Setup, [string]$App, [string]$Language, [string]$DataMode,
    [string]$RuntimeMode, [string]$RuntimeZip, [string]$LogPath) {
    $arguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LANG=' + $Language +
        ' /DIR="' + $App + '" /DATAMODE=' + $DataMode + ' /RUNTIMEMODE=' + $RuntimeMode +
        ' /TASKS="" /LOG="' + $LogPath + '"'
    if (-not [string]::IsNullOrWhiteSpace($RuntimeZip)) {
        $arguments += ' /RUNTIMEZIP="' + $RuntimeZip + '"'
    }
    $process = Start-Process $Setup -ArgumentList $arguments -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Setup exited with code $($process.ExitCode). Log: $LogPath" }
}

function Invoke-Uninstall([string]$App, [string]$LogPath) {
    $uninstaller = Join-Path $App 'unins000.exe'
    if (-not (Test-Path $uninstaller)) { throw "Uninstaller is missing: $uninstaller" }
    $arguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG="' + $LogPath + '"'
    $process = Start-Process $uninstaller -ArgumentList $arguments -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Uninstaller exited with code $($process.ExitCode). Log: $LogPath" }
}

function Test-InstalledRuntime([string]$App) {
    $manifestPath = Join-Path $App 'runtime\runtime-manifest.json'
    if (-not (Test-Path $manifestPath)) { throw "Installed runtime manifest is missing: $manifestPath" }
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $node = Join-Path (Join-Path $App 'runtime') $manifest.node
    $entry = Join-Path (Join-Path $App 'runtime') $manifest.entry
    if (-not (Test-Path $node)) { throw "Installed bundled Node is missing: $node" }
    if (-not (Test-Path $entry)) { throw "Installed runtime entry is missing: $entry" }
    return [pscustomobject]@{
        Version = $manifest.version
        Node = (& $node --version)
        Entry = $entry
    }
}

function Start-InstalledAppSmoke([string]$App, [string]$ConfigPath) {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        $port = ([Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }

    $config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
    $config.FirstRunCompleted = $true
    $config.Port = $port
    $config.Url = "http://127.0.0.1:$port"
    $config.LoadingStyle = 'off'
    $config.CloseAction = 'exit'
    $config.LaunchMode = 'window'
    [IO.File]::WriteAllText($ConfigPath, ($config | ConvertTo-Json -Compress), [Text.UTF8Encoding]::new($false))

    $appProcess = Start-Process (Join-Path $App 'dsh.exe') -WorkingDirectory $App -PassThru
    try {
        $nodeProcess = $null
        $response = $null
        for ($attempt = 0; $attempt -lt 180; $attempt++) {
            Start-Sleep -Milliseconds 500
            $appProcess.Refresh()
            if ($appProcess.HasExited) { break }
            $nodeProcess = Get-CimInstance Win32_Process | Where-Object {
                $_.Name -eq 'node.exe' -and $_.ParentProcessId -eq $appProcess.Id
            } | Select-Object -First 1
            try {
                $response = Invoke-WebRequest "http://127.0.0.1:$port" -UseBasicParsing -TimeoutSec 2
                if ($response.StatusCode -eq 200) { break }
            }
            catch {
            }
        }
        if (-not $response -or $response.StatusCode -ne 200) { throw "Installed application did not reach HTTP 200 on port $port" }
        if (-not $nodeProcess) { throw 'Installed application did not start its bundled Node process' }
        $expectedNode = Join-Path $App 'runtime\tools\node\node.exe'
        if (-not $nodeProcess.ExecutablePath.Equals($expectedNode, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Installed application used the wrong Node: $($nodeProcess.ExecutablePath)"
        }
        return [pscustomobject]@{
            HttpStatus = $response.StatusCode
            Port = $port
            BundledNode = $nodeProcess.ExecutablePath
            NodeWindowHandle = (Get-Process -Id $nodeProcess.ProcessId).MainWindowHandle
            DesktopPatch = $nodeProcess.CommandLine -match '--patch'
        }
    }
    finally {
        if (-not $appProcess.HasExited) {
            $appProcess.CloseMainWindow() | Out-Null
            if (-not $appProcess.WaitForExit(10000)) {
                & taskkill.exe /PID $appProcess.Id /T /F | Out-Null
            }
        }
    }
}

$fullResult = $null
$liteResult = $null
try {
    $env:LOCALAPPDATA = $fullLocal
    $fullInstallLog = Join-Path $testRootPath 'full-install.log'
    Invoke-Setup $full $fullApp 'chinesesimp' 'portable' 'bundled' '' $fullInstallLog
    $fullRuntime = Test-InstalledRuntime $fullApp
    $fullConfigPath = Join-Path $fullApp 'data\config.json'
    $fullConfig = Get-Content $fullConfigPath -Raw | ConvertFrom-Json
    if ($fullConfig.Language -ne 'zh-CN' -or $fullConfig.FirstRunCompleted) {
        throw 'Full Setup did not seed the expected Chinese first-run configuration'
    }
    if (-not (Test-Path (Join-Path $fullApp 'portable.mode'))) { throw 'Full Setup did not create portable.mode' }

    'portable user data' | Set-Content (Join-Path $fullApp 'data\user-keep.txt') -Encoding UTF8
    'user-owned root file' | Set-Content (Join-Path $fullApp 'user-owned.txt') -Encoding UTF8

    $fullUpgradeLog = Join-Path $testRootPath 'full-upgrade.log'
    Invoke-Setup $full $fullApp 'chinesesimp' 'portable' 'bundled' '' $fullUpgradeLog
    if (-not (Test-Path (Join-Path $fullApp 'data\user-keep.txt'))) { throw 'Full upgrade removed portable user data' }
    $fullAppSmoke = Start-InstalledAppSmoke $fullApp $fullConfigPath

    $fullUninstallLog = Join-Path $testRootPath 'full-uninstall.log'
    Invoke-Uninstall $fullApp $fullUninstallLog
    $fullResult = [pscustomobject]@{
        Language = 'zh-CN'
        PortableMarker = Test-Path (Join-Path $fullApp 'portable.mode')
        RuntimeVersion = $fullRuntime.Version
        RuntimeRemoved = -not (Test-Path (Join-Path $fullApp 'runtime'))
        UserDataPreserved = Test-Path (Join-Path $fullApp 'data\user-keep.txt')
        UserRootFilePreserved = Test-Path (Join-Path $fullApp 'user-owned.txt')
        AppSmoke = $fullAppSmoke
    }

    $env:LOCALAPPDATA = $liteLocal
    $liteInstallLog = Join-Path $testRootPath 'lite-install.log'
    Invoke-Setup $lite $liteApp 'english' 'standard' 'archive' $runtime $liteInstallLog
    $liteRuntime = Test-InstalledRuntime $liteApp
    $liteConfigPath = Join-Path $liteLocal 'DeepSeekHarness\config.json'
    $liteConfig = Get-Content $liteConfigPath -Raw | ConvertFrom-Json
    if ($liteConfig.Language -ne 'en-US' -or $liteConfig.FirstRunCompleted) {
        throw 'Lite Setup did not seed the expected English first-run configuration'
    }
    if (Test-Path (Join-Path $liteApp 'portable.mode')) { throw 'Lite standard install created portable.mode' }
    'standard user data' | Set-Content (Join-Path $liteLocal 'DeepSeekHarness\user-keep.txt') -Encoding UTF8

    $liteUninstallLog = Join-Path $testRootPath 'lite-uninstall.log'
    Invoke-Uninstall $liteApp $liteUninstallLog
    $liteResult = [pscustomobject]@{
        Language = 'en-US'
        PortableMarker = Test-Path (Join-Path $liteApp 'portable.mode')
        RuntimeVersion = $liteRuntime.Version
        RuntimeRemoved = -not (Test-Path (Join-Path $liteApp 'runtime'))
        UserDataPreserved = Test-Path (Join-Path $liteLocal 'DeepSeekHarness\user-keep.txt')
    }

    [pscustomobject]@{
        Root = $testRootPath
        Full = $fullResult
        Lite = $liteResult
    }
}
finally {
    $env:LOCALAPPDATA = $previousLocalAppData
}
