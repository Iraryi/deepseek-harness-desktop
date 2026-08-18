param(
    [string]$Archive = "$PSScriptRoot\dist\DeepSeek-Harness-Runtime-win-x64.zip"
)

$ErrorActionPreference = 'Stop'
$dist = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'dist'))
$archivePath = [IO.Path]::GetFullPath($Archive)
if (-not (Test-Path $archivePath)) { throw "Runtime archive not found: $archivePath" }

$extract = Join-Path $dist ('smoke-extract-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$extractPath = [IO.Path]::GetFullPath($extract)
if (-not $extractPath.StartsWith($dist + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Smoke extraction must stay under $dist"
}

New-Item -ItemType Directory -Path $extractPath | Out-Null
$process = $null
$ready = $false
try {
    & tar.exe -xf $archivePath -C $extractPath
    if ($LASTEXITCODE -ne 0) { throw "Runtime archive extraction failed: $LASTEXITCODE" }

    $manifestPath = Join-Path $extractPath 'runtime-manifest.json'
    if (-not (Test-Path $manifestPath)) { throw 'Runtime manifest is missing after extraction' }
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $node = Join-Path $extractPath $manifest.node
    $entry = Join-Path $extractPath $manifest.entry
    if (-not (Test-Path $node)) { throw "Bundled node is missing: $node" }
    if (-not (Test-Path $entry)) { throw "Runtime entry is missing: $entry" }

    $links = @(Get-ChildItem $extractPath -Recurse -Attributes ReparsePoint -ErrorAction SilentlyContinue)
    if ($links.Count -ne 0) { throw "Extracted runtime contains $($links.Count) reparse points" }

    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        $port = ([Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }

    $patch = Join-Path $extractPath 'desktop-smoke.patch.yml'
    @'
- id: directory-picker
  disabled: true
- insert:
    - id: directory-picker-browse
      name: '@deepseek-ai/dsh-host-directory-picker-browse'
    - id: ui-directory-picker-browse
      name: '@deepseek-ai/dsh-client-ui-directory-picker-browse'
'@ | Set-Content $patch -Encoding UTF8

    $stdout = Join-Path $extractPath 'stdout.log'
    $stderr = Join-Path $extractPath 'stderr.log'
    $process = Start-Process $node `
        -ArgumentList @($entry, 'web', '--patch', $patch, '--port', $port) `
        -WorkingDirectory $extractPath `
        -WindowStyle Hidden `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr `
        -PassThru

    $response = $null
    for ($attempt = 0; $attempt -lt 180; $attempt++) {
        Start-Sleep -Milliseconds 500
        $process.Refresh()
        if ($process.HasExited) { break }
        try {
            $response = Invoke-WebRequest "http://127.0.0.1:$port" -UseBasicParsing -TimeoutSec 2
            if ($response.StatusCode -eq 200) {
                $ready = $true
                break
            }
        }
        catch {
        }
    }

    $result = [pscustomobject]@{
        Archive = $archivePath
        ArchiveBytes = (Get-Item $archivePath).Length
        ExtractedBytes = (Get-ChildItem $extractPath -File -Recurse | Measure-Object Length -Sum).Sum
        RuntimeVersion = $manifest.version
        BundledNode = (& $node --version)
        EntryExists = Test-Path $entry
        ReparsePoints = $links.Count
        Ready = $ready
        HttpStatus = if ($ready) { $response.StatusCode } else { $null }
        NodeWindowHandle = $process.MainWindowHandle
        Stdout = Get-Content $stdout -Raw -ErrorAction SilentlyContinue
        Stderr = Get-Content $stderr -Raw -ErrorAction SilentlyContinue
    }
    $result
    if (-not $ready) {
        throw "Extracted runtime HTTP smoke failed.`nSTDOUT:`n$($result.Stdout)`nSTDERR:`n$($result.Stderr)`nPreserved: $extractPath"
    }
}
finally {
    if ($process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
    }
    if ($ready -and (Test-Path $extractPath)) {
        $resolved = [IO.Path]::GetFullPath($extractPath)
        if ($resolved.StartsWith($dist + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and
            (Split-Path $resolved -Leaf).StartsWith('smoke-extract-', [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    }
}
