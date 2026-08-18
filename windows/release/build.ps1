param(
    [string]$OutputDirectory = "$PSScriptRoot\dist",
    [string]$ReleaseBaseUrl = 'https://github.com/Iraryi/deepseek-harness-desktop/releases',
    [string]$InnoCompiler = '',
    [switch]$SkipProductBuild,
    [switch]$SkipSmoke
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$releaseRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$repository = [IO.Path]::GetFullPath((Join-Path $releaseRoot '..\..'))
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $output.StartsWith($releaseRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must stay under $releaseRoot"
}

$launcherRoot = Join-Path $repository 'windows\launcher'
$launcherDist = Join-Path $launcherRoot 'dist'
$runtimeRoot = Join-Path $repository 'windows\runtime'
$runtimeDist = Join-Path $runtimeRoot 'dist\runtime'
$runtimeArchive = Join-Path $runtimeRoot 'dist\DeepSeek-Harness-Runtime-win-x64.zip'
$setupRoot = Join-Path $repository 'windows\setup'
$setupDist = Join-Path $setupRoot 'dist'
$setupCache = Join-Path $setupRoot 'cache'
$package = Get-Content (Join-Path $repository 'package.json') -Raw | ConvertFrom-Json
$version = [string]$package.version
$tag = "v$version"

function Invoke-Checked([string]$Command, [string[]]$Arguments, [string]$WorkingDirectory) {
    Push-Location $WorkingDirectory
    try {
        & $Command @Arguments
        if ($LASTEXITCODE -ne 0) { throw "$Command exited with code $LASTEXITCODE" }
    }
    finally {
        Pop-Location
    }
}

function Get-MicrosoftBinary([string]$Url, [string]$Destination) {
    New-Item -ItemType Directory -Path (Split-Path $Destination -Parent) -Force | Out-Null
    if (-not (Test-Path $Destination)) {
        Invoke-WebRequest -Uri $Url -OutFile $Destination -UseBasicParsing
    }
    $signature = Get-AuthenticodeSignature -FilePath $Destination
    if ($signature.Status -ne 'Valid' -or -not $signature.SignerCertificate.Subject.Contains('Microsoft Corporation')) {
        throw "Downloaded file is not signed by Microsoft Corporation: $Destination"
    }
}

if (-not $SkipProductBuild) {
    Invoke-Checked 'powershell.exe' @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $launcherRoot 'build.ps1')
    ) $repository
    Invoke-Checked 'node.exe' @((Join-Path $runtimeRoot 'build.mjs')) $repository
}

$requiredLauncherFiles = @(
    'dsh.exe',
    'dsh-config.exe',
    'Microsoft.Web.WebView2.Core.dll',
    'Microsoft.Web.WebView2.WinForms.dll',
    'WebView2Loader.dll'
)
foreach ($name in $requiredLauncherFiles) {
    if (-not (Test-Path (Join-Path $launcherDist $name))) { throw "Launcher output is missing: $name" }
}
if (-not (Test-Path $runtimeDist)) { throw "Runtime directory is missing: $runtimeDist" }
if (-not (Test-Path $runtimeArchive)) { throw "Runtime archive is missing: $runtimeArchive" }

Get-MicrosoftBinary 'https://go.microsoft.com/fwlink/?linkid=2124701' `
    (Join-Path $setupCache 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe')
Get-MicrosoftBinary 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' `
    (Join-Path $setupCache 'MicrosoftEdgeWebview2Setup.exe')

$setupArguments = @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $setupRoot 'build.ps1'),
    '-ReleaseBaseUrl', $ReleaseBaseUrl
)
if (-not [string]::IsNullOrWhiteSpace($InnoCompiler)) {
    $setupArguments += @('-InnoCompiler', $InnoCompiler)
}
Invoke-Checked 'powershell.exe' $setupArguments $repository

if (-not $SkipSmoke) {
    Invoke-Checked 'powershell.exe' @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $setupRoot 'smoke.ps1')
    ) $repository
}

if (Test-Path $output) { Remove-Item -LiteralPath $output -Recurse -Force }
New-Item -ItemType Directory -Path $output | Out-Null

$fullName = "DeepSeek-Harness-Setup-Full-$version-win-x64.exe"
$liteName = "DeepSeek-Harness-Setup-Lite-$version-win-x64.exe"
$runtimeName = "DeepSeek-Harness-Runtime-$version-win-x64.zip"
$portableName = "DeepSeek-Harness-Portable-$version-win-x64.zip"
Copy-Item (Join-Path $setupDist $fullName) (Join-Path $output $fullName)
Copy-Item (Join-Path $setupDist $liteName) (Join-Path $output $liteName)
Copy-Item $runtimeArchive (Join-Path $output $runtimeName)
Copy-Item (Join-Path $releaseRoot 'download.ps1') (Join-Path $output 'Install-DeepSeek-Harness.ps1')
Copy-Item (Join-Path $releaseRoot 'release-notes.txt') (Join-Path $output 'RELEASE_NOTES.md')

$portableStage = Join-Path $releaseRoot ('.portable-stage-' + [Guid]::NewGuid().ToString('N').Substring(0, 12))
try {
    New-Item -ItemType Directory -Path $portableStage | Out-Null
    foreach ($name in $requiredLauncherFiles) {
        Copy-Item (Join-Path $launcherDist $name) $portableStage
    }
    $portableRuntime = Join-Path $portableStage 'runtime'
    New-Item -ItemType Directory -Path $portableRuntime | Out-Null
    Get-ChildItem -LiteralPath $runtimeDist -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $portableRuntime -Recurse -Force
    }
    New-Item -ItemType File -Path (Join-Path $portableStage 'portable.mode') | Out-Null
    Copy-Item (Join-Path $releaseRoot 'PORTABLE-README.txt') $portableStage
    Invoke-Checked 'tar.exe' @('-a', '-c', '-f', (Join-Path $output $portableName), '-C', $portableStage, '.') $repository
}
finally {
    if (Test-Path $portableStage) { Remove-Item -LiteralPath $portableStage -Recurse -Force }
}

$assetFiles = @(Get-ChildItem -LiteralPath $output -File | Sort-Object Name)
$assets = @($assetFiles | ForEach-Object {
    [ordered]@{
        name = $_.Name
        bytes = $_.Length
        sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
})
$manifestPath = Join-Path $output 'release-manifest.json'
[ordered]@{
    schemaVersion = 1
    product = 'DeepSeek Harness Desktop'
    repository = 'Iraryi/deepseek-harness-desktop'
    version = $version
    tag = $tag
    platform = 'win-x64'
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    assets = $assets
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

$checksumFiles = @(Get-ChildItem -LiteralPath $output -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | Sort-Object Name)
$checksumLines = @($checksumFiles | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
})
$checksumLines | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ASCII

if (-not $SkipSmoke) {
    Invoke-Checked 'powershell.exe' @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $releaseRoot 'smoke.ps1'),
        '-ReleaseDirectory', $output
    ) $repository
}

[pscustomobject]@{
    Version = $version
    Tag = $tag
    Output = $output
    Assets = @(Get-ChildItem -LiteralPath $output -File | Select-Object Name, Length)
}
