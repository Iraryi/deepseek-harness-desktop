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
$runtimeSha256 = (Get-FileHash -LiteralPath $runtime -Algorithm SHA256).Hash.ToLowerInvariant()

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
        ' /DIR="' + $App + '" /TASKS="" /LOG="' + $LogPath + '"'
    if (-not [string]::IsNullOrWhiteSpace($DataMode)) {
        $arguments += ' /DATAMODE=' + $DataMode
    }
    if (-not [string]::IsNullOrWhiteSpace($RuntimeMode)) {
        $arguments += ' /RUNTIMEMODE=' + $RuntimeMode
    }
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
    $npm = Join-Path (Join-Path $App 'runtime') $manifest.packageManager.command
    $npmCli = Join-Path (Join-Path $App 'runtime') $manifest.packageManager.cli
    $entry = Join-Path (Join-Path $App 'runtime') $manifest.entry
    if (-not (Test-Path $node)) { throw "Installed bundled Node is missing: $node" }
    if (-not (Test-Path $npm)) { throw "Installed bundled npm command is missing: $npm" }
    if (-not (Test-Path $npmCli)) { throw "Installed bundled npm CLI is missing: $npmCli" }
    if (-not (Test-Path $entry)) { throw "Installed runtime entry is missing: $entry" }
    return [pscustomobject]@{
        Version = $manifest.version
        Node = (& $node --version)
        Npm = (& $node $npmCli --version)
        Entry = $entry
    }
}

function Start-BundledNodeLock([string]$App) {
    $node = Join-Path $App 'runtime\tools\node\node.exe'
    $process = Start-Process -FilePath $node -WorkingDirectory $App `
        -ArgumentList @('-e', 'setInterval(()=>{},1000)') -PassThru -WindowStyle Hidden
    Start-Sleep -Milliseconds 750
    $process.Refresh()
    if ($process.HasExited) { throw "Bundled Node lock process exited early with code $($process.ExitCode)" }
    return $process
}

function Assert-ProcessExited($Process, [string]$Operation) {
    if (-not $Process.WaitForExit(10000)) {
        Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue
        throw "$Operation did not stop bundled Node process $($Process.Id)"
    }
}

function Remove-SmokeRoot {
    if (-not (Test-Path -LiteralPath $testRootPath)) { return }
    for ($attempt = 0; $attempt -lt 10; $attempt++) {
        try {
            Remove-Item -LiteralPath $testRootPath -Recurse -Force -ErrorAction Stop
            return
        }
        catch {
            if ($attempt -eq 9) { throw }
            Start-Sleep -Milliseconds 250
        }
    }
}

function Stop-SmokeProcessTree($AppProcess, [string]$App) {
    if ($AppProcess) {
        try { $AppProcess.Refresh() } catch {}
        if (-not $AppProcess.HasExited) {
            $AppProcess.CloseMainWindow() | Out-Null
            if (-not $AppProcess.WaitForExit(10000)) {
                & taskkill.exe /PID $AppProcess.Id /T /F | Out-Null
            }
        }
    }
    Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object {
        $_.ExecutablePath -and
        $_.ExecutablePath.StartsWith($App + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
    } | ForEach-Object {
        Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Milliseconds 500
}

function Start-InstalledAppSmoke([string]$App, [string]$ConfigPath) {
    $previousScope = $env:DEEPSEEK_HARNESS_INSTANCE_SCOPE
    $previousDataDirectory = $env:DEEPSEEK_HARNESS_DATA_DIR
    $failures = @()
    try {
        $env:DEEPSEEK_HARNESS_DATA_DIR = Split-Path $ConfigPath -Parent
        for ($launchAttempt = 1; $launchAttempt -le 3; $launchAttempt++) {
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

            $env:DEEPSEEK_HARNESS_INSTANCE_SCOPE = 'setup-smoke-' + [Guid]::NewGuid().ToString('N')
            $appProcess = $null
            $nodeProcess = $null
            $response = $null
            $lastWebError = $null
            try {
                $dataRoot = Split-Path $ConfigPath -Parent
                $appLog = Join-Path $dataRoot 'logs\app.log'
                Remove-Item -LiteralPath $appLog -Force -ErrorAction SilentlyContinue
                $appProcess = Start-Process (Join-Path $App 'dsh.exe') -WorkingDirectory $App -PassThru
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
                        $lastWebError = $_.Exception.Message
                    }
                }

                if ($response -and $response.StatusCode -eq 200) {
                    if (-not $nodeProcess) { throw 'Installed application did not start its bundled Node process' }
                    $expectedNode = Join-Path $App 'runtime\tools\node\node.exe'
                    if (-not $nodeProcess.ExecutablePath.Equals($expectedNode, [StringComparison]::OrdinalIgnoreCase)) {
                        throw "Installed application used the wrong Node: $($nodeProcess.ExecutablePath)"
                    }
                    if ($nodeProcess.CommandLine -notmatch '--import.+runtime-resolver\.mjs') {
                        throw "Installed application did not load the packaged Runtime resolver: $($nodeProcess.CommandLine)"
                    }
                    $launcherReady = $false
                    $pageLoaded = $false
                    $webUiVerified = $false
                    $webUiFailed = $false
                    $logDeadline = (Get-Date).AddSeconds(45)
                    while ((Get-Date) -lt $logDeadline) {
                        if (Test-Path -LiteralPath $appLog) {
                            $logText = Get-Content -LiteralPath $appLog -Raw
                            $readyIndex = $logText.IndexOf('Plugin graph ready: ', [StringComparison]::Ordinal)
                            $pageIndex = $logText.IndexOf('Page loaded: ', [StringComparison]::Ordinal)
                            $launcherReady = $readyIndex -ge 0
                            $pageLoaded = $pageIndex -ge 0
                            $webUiVerified = $logText.Contains('Web UI boot verified by structured ready status')
                            $webUiFailed = $logText.Contains('Web UI boot failure detected: Failed to load plugins')
                            if (($webUiVerified -and $pageLoaded) -or $webUiFailed) { break }
                        }
                        Start-Sleep -Milliseconds 250
                    }
                    if (-not $launcherReady) { throw 'Installed launcher did not observe the settled plugin graph' }
                    if (-not $pageLoaded) { throw 'Installed launcher did not complete its WebView2 navigation' }
                    if ($webUiFailed) { throw 'Installed WebView2 rendered the Failed to load plugins page' }
                    if (-not $webUiVerified) { throw 'Installed WebView2 did not verify a successful plugin boot' }
                    return [pscustomobject]@{
                        HttpStatus = $response.StatusCode
                        Port = $port
                        BundledNode = $nodeProcess.ExecutablePath
                        NodeWindowHandle = (Get-Process -Id $nodeProcess.ProcessId).MainWindowHandle
                        DesktopPatch = $nodeProcess.CommandLine -match '--patch'
                        RuntimeResolver = $nodeProcess.CommandLine -match '--import.+runtime-resolver\.mjs'
                        PluginGraphReady = $launcherReady
                        PageLoaded = $pageLoaded
                        WebUiPluginBootVerified = $webUiVerified
                    }
                }

                $processState = if ($appProcess.HasExited) { "exited=$($appProcess.ExitCode)" } else { 'running' }
                $nodeState = if ($nodeProcess) { "node=$($nodeProcess.ProcessId)" } else { 'node=missing' }
                $failure = "attempt=$launchAttempt port=$port app=$processState $nodeState web=$lastWebError"
                $failures += $failure
                Write-Warning "Installed application smoke retry: $failure"
                $logRoot = Join-Path $dataRoot 'logs'
                Get-ChildItem -LiteralPath $logRoot -File -ErrorAction SilentlyContinue | ForEach-Object {
                    Write-Warning "Log tail: $($_.FullName)"
                    Get-Content -LiteralPath $_.FullName -Tail 40 -ErrorAction SilentlyContinue | ForEach-Object { Write-Warning $_ }
                }
            }
            finally {
                Stop-SmokeProcessTree $appProcess $App
            }
        }
    }
    finally {
        $env:DEEPSEEK_HARNESS_INSTANCE_SCOPE = $previousScope
        $env:DEEPSEEK_HARNESS_DATA_DIR = $previousDataDirectory
    }
    throw "Installed application did not reach HTTP 200 after 3 attempts: $($failures -join '; ')"
}

$fullResult = $null
$liteResult = $null
try {
    $env:LOCALAPPDATA = $fullLocal
    $fullInstallLog = Join-Path $testRootPath 'full-install.log'
    Invoke-Setup $full $fullApp 'chinesesimp' '' '' '' $fullInstallLog
    foreach ($launcherName in @('dsh.exe', 'dsh-hub.exe', 'dsh-config.exe')) {
        if (-not (Test-Path (Join-Path $fullApp $launcherName))) {
            throw "Full Setup did not install $launcherName"
        }
    }
    $fullRuntime = Test-InstalledRuntime $fullApp
    $fullRuntimeSource = (Get-Content -LiteralPath (Join-Path $fullApp 'runtime\.source-sha256') -Raw).Trim()
    if (-not $fullRuntimeSource.Equals($runtimeSha256, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Full Setup did not record the installed Runtime archive identity'
    }
    $fullConfigPath = Join-Path $fullLocal 'DeepSeekHarness\config.json'
    $fullConfig = Get-Content $fullConfigPath -Raw | ConvertFrom-Json
    if ($fullConfig.Language -ne 'zh-CN' -or $fullConfig.FirstRunCompleted) {
        throw 'Full Setup did not seed the expected Chinese first-run configuration'
    }
    if (Test-Path (Join-Path $fullApp 'portable.mode')) { throw 'Recommended Full Setup created portable.mode' }

    'standard user data' | Set-Content (Join-Path $fullLocal 'DeepSeekHarness\user-keep.txt') -Encoding UTF8
    'user-owned root file' | Set-Content (Join-Path $fullApp 'user-owned.txt') -Encoding UTF8

    if (-not (Test-Path (Join-Path $fullApp 'setup\stop-installed-processes.ps1'))) {
        throw 'Full Setup did not install its uninstall process-cleanup helper'
    }
    $upgradeLock = Start-BundledNodeLock $fullApp
    $fullUpgradeLog = Join-Path $testRootPath 'full-upgrade.log'
    Invoke-Setup $full $fullApp 'chinesesimp' '' '' '' $fullUpgradeLog
    Assert-ProcessExited $upgradeLock 'Full upgrade'
    if (-not (Test-Path (Join-Path $fullLocal 'DeepSeekHarness\user-keep.txt'))) { throw 'Full upgrade removed standard user data' }
    $fullAppSmoke = Start-InstalledAppSmoke $fullApp $fullConfigPath
    $processJobSmoke = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
        (Join-Path $PSScriptRoot '..\launcher\smoke-process-job.ps1') -AppDirectory $fullApp -Runs 3

    $uninstallLock = Start-BundledNodeLock $fullApp
    $fullUninstallLog = Join-Path $testRootPath 'full-uninstall.log'
    Invoke-Uninstall $fullApp $fullUninstallLog
    Assert-ProcessExited $uninstallLock 'Full uninstall'
    if (Test-Path (Join-Path $fullApp 'runtime')) { throw 'Full uninstall left the packaged Runtime behind' }
    $fullResult = [pscustomobject]@{
        Language = 'zh-CN'
        RecommendedDefaults = $true
        PortableMarker = Test-Path (Join-Path $fullApp 'portable.mode')
        RuntimeVersion = $fullRuntime.Version
        RuntimeRemoved = -not (Test-Path (Join-Path $fullApp 'runtime'))
        UserDataPreserved = Test-Path (Join-Path $fullLocal 'DeepSeekHarness\user-keep.txt')
        UserRootFilePreserved = Test-Path (Join-Path $fullApp 'user-owned.txt')
        AppSmoke = $fullAppSmoke
        ProcessJobSmoke = $processJobSmoke
        UpgradeStoppedLockedNode = $upgradeLock.HasExited
        UninstallStoppedLockedNode = $uninstallLock.HasExited
    }

    $env:LOCALAPPDATA = $liteLocal
    $liteInstallLog = Join-Path $testRootPath 'lite-install.log'
    Invoke-Setup $lite $liteApp 'english' 'portable' 'archive' $runtime $liteInstallLog
    foreach ($launcherName in @('dsh.exe', 'dsh-hub.exe', 'dsh-config.exe')) {
        if (-not (Test-Path (Join-Path $liteApp $launcherName))) {
            throw "Lite Setup did not install $launcherName"
        }
    }
    $liteRuntime = Test-InstalledRuntime $liteApp
    $liteConfigPath = Join-Path $liteApp 'data\config.json'
    $liteConfig = Get-Content $liteConfigPath -Raw | ConvertFrom-Json
    if ($liteConfig.Language -ne 'en-US' -or $liteConfig.FirstRunCompleted) {
        throw 'Lite Setup did not seed the expected English first-run configuration'
    }
    if (-not (Test-Path (Join-Path $liteApp 'portable.mode'))) { throw 'Lite portable install did not create portable.mode' }
    'portable user data' | Set-Content (Join-Path $liteApp 'data\user-keep.txt') -Encoding UTF8

    $liteUninstallLog = Join-Path $testRootPath 'lite-uninstall.log'
    Invoke-Uninstall $liteApp $liteUninstallLog
    if (Test-Path (Join-Path $liteApp 'runtime')) { throw 'Lite uninstall left the packaged Runtime behind' }
    $liteResult = [pscustomobject]@{
        Language = 'en-US'
        PortableMarker = Test-Path (Join-Path $liteApp 'portable.mode')
        RuntimeVersion = $liteRuntime.Version
        RuntimeRemoved = -not (Test-Path (Join-Path $liteApp 'runtime'))
        UserDataPreserved = Test-Path (Join-Path $liteApp 'data\user-keep.txt')
    }

    [pscustomobject]@{
        Root = $testRootPath
        Full = $fullResult
        Lite = $liteResult
    }
}
finally {
    $env:LOCALAPPDATA = $previousLocalAppData
    Remove-SmokeRoot
}
