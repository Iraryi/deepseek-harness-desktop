param(
    [string]$OutputDirectory = "$PSScriptRoot\dist",
    [string]$WebView2Package = ''
)

$ErrorActionPreference = 'Stop'
$webView2Version = '1.0.4129.50'
$launcherRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $output.StartsWith($launcherRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must stay under $launcherRoot"
}
if (Test-Path $output) { Remove-Item -LiteralPath $output -Recurse -Force }
New-Item -ItemType Directory -Path $output | Out-Null

$repository = [IO.Path]::GetFullPath((Join-Path $launcherRoot '..\..'))
$package = Get-Content (Join-Path $repository 'package.json') -Raw | ConvertFrom-Json
$version = [string]$package.version
$numeric = [regex]::Matches($version, '\d+') | ForEach-Object { [int]$_.Value }
while ($numeric.Count -lt 4) { $numeric = @($numeric) + 0 }
$assemblyVersion = '{0}.{1}.{2}.0' -f $numeric[0], $numeric[1], $numeric[2]
$fileVersion = '{0}.{1}.{2}.{3}' -f $numeric[0], $numeric[1], $numeric[2], $numeric[3]
$generatedAssembly = Join-Path $output 'AssemblyVersion.g.cs'
@"
using System.Reflection;
[assembly: AssemblyVersion("$assemblyVersion")]
[assembly: AssemblyFileVersion("$fileVersion")]
[assembly: AssemblyInformationalVersion("$version")]
"@ | Set-Content -LiteralPath $generatedAssembly -Encoding UTF8

$cache = Join-Path $launcherRoot 'cache'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
if ([string]::IsNullOrWhiteSpace($WebView2Package)) {
    $WebView2Package = Join-Path $cache "Microsoft.Web.WebView2.$webView2Version.nupkg"
}
if (-not (Test-Path $WebView2Package)) {
    $url = "https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2/$webView2Version"
    try {
        Invoke-WebRequest -Uri $url -OutFile $WebView2Package -UseBasicParsing
    }
    catch {
        throw "WebView2 download failed. Download Microsoft.Web.WebView2 $webView2Version manually and pass -WebView2Package <file.nupkg>. $($_.Exception.Message)"
    }
}

$packageDirectory = Join-Path $cache "Microsoft.Web.WebView2.$webView2Version"
if (-not (Test-Path (Join-Path $packageDirectory 'lib\net462\Microsoft.Web.WebView2.Core.dll'))) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path $packageDirectory) { Remove-Item -LiteralPath $packageDirectory -Recurse -Force }
    [IO.Compression.ZipFile]::ExtractToDirectory([IO.Path]::GetFullPath($WebView2Package), $packageDirectory)
}

$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $compiler)) { throw ".NET Framework x64 compiler not found: $compiler" }
$source = Join-Path $launcherRoot 'src'
$webViewLib = Join-Path $packageDirectory 'lib\net462'
$icon = Join-Path $launcherRoot 'assets\dsh.ico'
$commonSources = @(
    (Join-Path $source 'AssemblyInfo.cs'),
    $generatedAssembly,
    (Join-Path $source 'Config.cs')
)
$commonReferences = @(
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Web.Extensions.dll'
)

& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/win32icon:$icon" "/out:$output\dsh.exe" `
    @commonReferences "/reference:$webViewLib\Microsoft.Web.WebView2.Core.dll" `
    "/reference:$webViewLib\Microsoft.Web.WebView2.WinForms.dll" @commonSources (Join-Path $source 'MainApp.cs')
if ($LASTEXITCODE -ne 0) { throw "dsh.exe compilation failed: $LASTEXITCODE" }

& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/win32icon:$icon" "/out:$output\dsh-config.exe" `
    @commonReferences @commonSources (Join-Path $source 'ConfigApp.cs')
if ($LASTEXITCODE -ne 0) { throw "dsh-config.exe compilation failed: $LASTEXITCODE" }

Copy-Item (Join-Path $webViewLib 'Microsoft.Web.WebView2.Core.dll') $output
Copy-Item (Join-Path $webViewLib 'Microsoft.Web.WebView2.WinForms.dll') $output
Copy-Item (Join-Path $packageDirectory 'runtimes\win-x64\native\WebView2Loader.dll') $output
Remove-Item -LiteralPath $generatedAssembly -Force

$expected = @('dsh.exe', 'dsh-config.exe', 'Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.WinForms.dll', 'WebView2Loader.dll')
foreach ($name in $expected) {
    if (-not (Test-Path (Join-Path $output $name))) { throw "Missing launcher output: $name" }
}
Write-Host "Launcher $version built at $output"
