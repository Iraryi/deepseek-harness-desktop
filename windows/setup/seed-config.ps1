param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('zh-CN', 'en-US')]
    [string]$Language,

    [Parameter(Mandatory = $true)]
    [string]$AppDirectory,

    [switch]$Portable
)

$ErrorActionPreference = 'Stop'
$app = [IO.Path]::GetFullPath($AppDirectory)
$data = if ($Portable) { Join-Path $app 'data' } else { Join-Path $env:LOCALAPPDATA 'DeepSeekHarness' }
$configPath = Join-Path $data 'config.json'
New-Item -ItemType Directory -Path $data -Force | Out-Null
$created = $false

if (-not (Test-Path $configPath)) {
    $config = [ordered]@{
        ResolutionWidth = 1280
        ResolutionHeight = 800
        Language = $Language
        FirstRunCompleted = $false
        LaunchMode = 'window'
        Url = 'http://127.0.0.1:3080'
        Port = 3080
        NodePath = ''
        RepoPath = ''
        ToolbarAutoHide = $true
        ToolbarEdgeReveal = $false
        ToolbarHotkey = 'F8'
        FullscreenHotkey = 'F11'
        LoadingStyle = 'whales'
        CloseAction = 'tray'
        ShowTrayButton = $true
        FullscreenShowToolbar = $false
        FullscreenShowTaskbar = $false
        EnableExtensions = $false
        Extensions = @()
        InjectCss = ''
        InjectJs = ''
        DevTools = $true
        ExternalLinksInBrowser = $true
    }
    $json = $config | ConvertTo-Json -Compress
    [IO.File]::WriteAllText($configPath, $json, [Text.UTF8Encoding]::new($false))
    $created = $true
}

[pscustomobject]@{
    Config = $configPath
    Created = $created
    Portable = [bool]$Portable
} | ConvertTo-Json -Compress
