#ifndef AppVersion
  #define AppVersion "0.1.0-rc.5"
#endif
#ifndef AppNumericVersion
  #define AppNumericVersion "0.1.0.5"
#endif
#ifndef LauncherDir
  #define LauncherDir "..\launcher\dist"
#endif
#ifndef OutputDir
  #define OutputDir "dist"
#endif
#ifndef RuntimeArchive
  #define RuntimeArchive "..\runtime\dist\DeepSeek-Harness-Runtime-win-x64.zip"
#endif
#ifndef RuntimeAssetName
  #define RuntimeAssetName "DeepSeek-Harness-Runtime-" + AppVersion + "-win-x64.zip"
#endif
#ifndef RuntimeSha256
  #define RuntimeSha256 ""
#endif
#ifndef ReleaseBaseUrl
  #define ReleaseBaseUrl "https://github.com/Iraryi/deepseek-harness-desktop/releases"
#endif
#ifndef WebViewOffline
  #define WebViewOffline "cache\MicrosoftEdgeWebView2RuntimeInstallerX64.exe"
#endif
#ifndef WebViewBootstrapper
  #define WebViewBootstrapper "cache\MicrosoftEdgeWebview2Setup.exe"
#endif
#ifndef RepositoryRoot
  #define RepositoryRoot "..\.."
#endif

#if Flavor == "full"
  #define FlavorTitle "Full"
  #define FlavorDefaultMode "bundled"
#else
  #define FlavorTitle "Lite"
  #define FlavorDefaultMode "download"
#endif

#define RuntimeDownloadUrl ReleaseBaseUrl + "/download/v" + AppVersion + "/" + RuntimeAssetName

[Setup]
AppId={{C6E80677-0378-4C12-99F5-C69665A59B6E}
AppName=DeepSeek Harness
AppVersion={#AppVersion}
AppVerName=DeepSeek Harness Desktop {#AppVersion}
AppPublisher=DeepSeek Harness Desktop contributors
AppPublisherURL=https://github.com/Iraryi/deepseek-harness-desktop
AppSupportURL=https://github.com/Iraryi/deepseek-harness-desktop/issues
AppUpdatesURL=https://github.com/Iraryi/deepseek-harness-desktop/releases
VersionInfoVersion={#AppNumericVersion}
VersionInfoProductVersion={#AppNumericVersion}
VersionInfoCompany=DeepSeek AI
VersionInfoDescription=DeepSeek Harness {#FlavorTitle} Setup
VersionInfoProductName=DeepSeek Harness Desktop
DefaultDirName={localappdata}\Programs\DeepSeek Harness
DefaultGroupName=DeepSeek Harness
DisableProgramGroupPage=auto
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
WizardStyle=modern dynamic
WizardResizable=yes
WizardSizePercent=120
Compression=lzma2/normal
SolidCompression=yes
SetupIconFile={#RepositoryRoot}\windows\launcher\assets\dsh.ico
UninstallDisplayIcon={app}\dsh.exe
LicenseFile={#RepositoryRoot}\LICENSE
OutputDir={#OutputDir}
OutputBaseFilename=DeepSeek-Harness-Setup-{#FlavorTitle}-{#AppVersion}-win-x64
UsePreviousAppDir=yes
UsePreviousLanguage=yes
UsePreviousTasks=yes
CloseApplications=yes
RestartApplications=no
CloseApplicationsFilter=dsh.exe,dsh-config.exe
AppMutex=Local\DeepSeekHarness.Desktop.DEFAULT
SetupLogging=yes
ChangesEnvironment=no
ChangesAssociations=no
DisableWelcomePage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"

[CustomMessages]
english.DataModeTitle=Application data
english.DataModeDescription=Choose where settings, logs, and browser data are stored.
english.DataModePrompt=Standard mode stores data in %%LOCALAPPDATA%%. Portable data mode stores it beside the application and keeps it after uninstall.
english.DataModeStandard=Standard data mode (recommended)
english.DataModePortable=Portable data mode
english.RuntimeModeTitle=Runtime source
english.RuntimeModeDescription=Choose how Setup obtains the DeepSeek Harness runtime.
english.RuntimeModePrompt=Full Setup works offline by default. Lite Setup downloads the verified runtime by default. Local alternatives are always available.
english.RuntimeBundled=Use the runtime included in this Setup (offline, recommended)
english.RuntimeDownload=Download the verified runtime from GitHub (recommended)
english.RuntimeArchive=Import a local Runtime ZIP
english.RuntimeFolder=Copy an existing Runtime folder
english.RuntimeSource=Build from a GitHub source ZIP (advanced; requires Node.js and pnpm)
english.RuntimeArchiveTitle=Local Runtime ZIP
english.RuntimeArchiveDescription=Select a previously downloaded DeepSeek Harness Runtime ZIP.
english.RuntimeArchivePrompt=Runtime ZIP:
english.RuntimeFolderTitle=Existing Runtime folder
english.RuntimeFolderDescription=Select a folder containing runtime-manifest.json, tools\node\node.exe, and the packaged application.
english.RuntimeFolderPrompt=Runtime folder:
english.SourceArchiveTitle=Source ZIP build
english.SourceArchiveDescription=Select a DeepSeek Harness GitHub source ZIP. Setup will install dependencies and build it locally.
english.SourceArchivePrompt=Source ZIP:
english.MissingRuntimeArchive=Select an existing Runtime ZIP before continuing.
english.MissingRuntimeFolder=Select an existing Runtime folder before continuing.
english.MissingSourceArchive=Select an existing source ZIP before continuing.
english.DownloadTitle=Downloading DeepSeek Harness
english.DownloadDescription=Setup is downloading and verifying the Windows Runtime.
english.DownloadFailed=The Runtime download failed
english.RuntimeInstallFailed=The selected Runtime could not be installed. Setup has preserved the previous Runtime when one existed.
english.ConfigSeedFailed=Setup could not prepare the first-run configuration.
english.WebViewInstallFailed=Microsoft Edge WebView2 Runtime could not be installed.
english.ReadyDataMode=Data mode:
english.ReadyRuntimeMode=Runtime source:
english.ReadyDataStandard=Standard — %%LOCALAPPDATA%%\DeepSeekHarness
english.ReadyDataPortable=Portable — application directory\data (preserved on uninstall)
english.WebViewPresent=Microsoft Edge WebView2 Runtime is already installed.
english.WebViewMissing=Microsoft Edge WebView2 Runtime will be installed before first launch.

chinesesimp.DataModeTitle=应用数据
chinesesimp.DataModeDescription=选择设置、日志和浏览器数据的保存位置。
chinesesimp.DataModePrompt=标准模式把数据保存到 %%LOCALAPPDATA%%；便携数据模式把数据放在程序旁边，卸载时仍会保留。
chinesesimp.DataModeStandard=标准数据模式（推荐）
chinesesimp.DataModePortable=便携数据模式
chinesesimp.RuntimeModeTitle=运行时来源
chinesesimp.RuntimeModeDescription=选择安装程序如何取得 DeepSeek Harness Runtime。
chinesesimp.RuntimeModePrompt=Full 安装包默认完全离线；Lite 安装包默认从 GitHub 下载并校验。始终可以改用本地文件。
chinesesimp.RuntimeBundled=使用安装包内置 Runtime（离线，推荐）
chinesesimp.RuntimeDownload=从 GitHub 下载并校验 Runtime（推荐）
chinesesimp.RuntimeArchive=导入本地 Runtime ZIP
chinesesimp.RuntimeFolder=复制已有 Runtime 文件夹
chinesesimp.RuntimeSource=从 GitHub 源码 ZIP 本地构建（高级；需要 Node.js 与 pnpm）
chinesesimp.RuntimeArchiveTitle=本地 Runtime ZIP
chinesesimp.RuntimeArchiveDescription=选择以前下载的 DeepSeek Harness Runtime ZIP。
chinesesimp.RuntimeArchivePrompt=Runtime ZIP：
chinesesimp.RuntimeFolderTitle=已有 Runtime 文件夹
chinesesimp.RuntimeFolderDescription=选择包含 runtime-manifest.json、tools\node\node.exe 和打包程序的文件夹。
chinesesimp.RuntimeFolderPrompt=Runtime 文件夹：
chinesesimp.SourceArchiveTitle=源码 ZIP 构建
chinesesimp.SourceArchiveDescription=选择 DeepSeek Harness 的 GitHub 源码 ZIP，安装程序会在本机安装依赖并构建。
chinesesimp.SourceArchivePrompt=源码 ZIP：
chinesesimp.MissingRuntimeArchive=请先选择一个存在的 Runtime ZIP。
chinesesimp.MissingRuntimeFolder=请先选择一个存在的 Runtime 文件夹。
chinesesimp.MissingSourceArchive=请先选择一个存在的源码 ZIP。
chinesesimp.DownloadTitle=正在下载 DeepSeek Harness
chinesesimp.DownloadDescription=安装程序正在下载并校验 Windows Runtime。
chinesesimp.DownloadFailed=Runtime 下载失败
chinesesimp.RuntimeInstallFailed=所选 Runtime 无法安装；如果此前已有 Runtime，安装程序已经将其保留。
chinesesimp.ConfigSeedFailed=安装程序无法准备首次运行配置。
chinesesimp.WebViewInstallFailed=无法安装 Microsoft Edge WebView2 Runtime。
chinesesimp.ReadyDataMode=数据模式：
chinesesimp.ReadyRuntimeMode=Runtime 来源：
chinesesimp.ReadyDataStandard=标准 — %%LOCALAPPDATA%%\DeepSeekHarness
chinesesimp.ReadyDataPortable=便携 — 程序目录\data（卸载时保留）
chinesesimp.WebViewPresent=系统已经安装 Microsoft Edge WebView2 Runtime。
chinesesimp.WebViewMissing=首次启动前将安装 Microsoft Edge WebView2 Runtime。

[Files]
Source: "{#LauncherDir}\dsh.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#LauncherDir}\dsh-config.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#LauncherDir}\Microsoft.Web.WebView2.Core.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#LauncherDir}\Microsoft.Web.WebView2.WinForms.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#LauncherDir}\WebView2Loader.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "install-runtime.ps1"; Flags: dontcopy noencryption
Source: "seed-config.ps1"; Flags: dontcopy noencryption
#if Flavor == "full"
Source: "{#RuntimeArchive}"; Flags: dontcopy noencryption
Source: "{#WebViewOffline}"; Flags: dontcopy noencryption
#else
Source: "{#WebViewBootstrapper}"; Flags: dontcopy noencryption
#endif

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Icons]
Name: "{group}\DeepSeek Harness"; Filename: "{app}\dsh.exe"; WorkingDir: "{app}"
Name: "{group}\DeepSeek Harness CONFIG"; Filename: "{app}\dsh-config.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\DeepSeek Harness"; Filename: "{app}\dsh.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\dsh.exe"; Description: "{cm:LaunchProgram,DeepSeek Harness}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\runtime"
Type: files; Name: "{app}\portable.mode"

[Code]
var
  DataModePage: TInputOptionWizardPage;
  RuntimeModePage: TInputOptionWizardPage;
  RuntimeArchivePage: TInputFileWizardPage;
  RuntimeFolderPage: TInputDirWizardPage;
  SourceArchivePage: TInputFileWizardPage;
  DownloadPage: TDownloadWizardPage;
  PreparationComplete: Boolean;

function RuntimeModeKey: String;
begin
  case RuntimeModePage.SelectedValueIndex of
#if Flavor == "full"
    0: Result := 'bundled';
#else
    0: Result := 'download';
#endif
    1: Result := 'archive';
    2: Result := 'folder';
    3: Result := 'source';
  else
    Result := '{#FlavorDefaultMode}';
  end;
end;

function DataModeKey: String;
begin
  if DataModePage.SelectedValueIndex = 1 then
    Result := 'portable'
  else
    Result := 'standard';
end;

function RuntimeModeDisplay: String;
var
  Mode: String;
begin
  Mode := RuntimeModeKey;
  if Mode = 'bundled' then Result := CustomMessage('RuntimeBundled')
  else if Mode = 'download' then Result := CustomMessage('RuntimeDownload')
  else if Mode = 'archive' then Result := CustomMessage('RuntimeArchive')
  else if Mode = 'folder' then Result := CustomMessage('RuntimeFolder')
  else Result := CustomMessage('RuntimeSource');
end;

function IsWebView2Installed: Boolean;
var
  Version: String;
  Key: String;
begin
  Key := 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  Result :=
    (RegQueryStringValue(HKLM32, Key, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0')) or
    (RegQueryStringValue(HKCU32, Key, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0'));
end;

procedure SelectRuntimeMode(const Mode: String);
begin
#if Flavor == "full"
  if Mode = 'bundled' then RuntimeModePage.SelectedValueIndex := 0
#else
  if Mode = 'download' then RuntimeModePage.SelectedValueIndex := 0
#endif
  else if Mode = 'archive' then RuntimeModePage.SelectedValueIndex := 1
  else if Mode = 'folder' then RuntimeModePage.SelectedValueIndex := 2
  else if Mode = 'source' then RuntimeModePage.SelectedValueIndex := 3;
end;

procedure InitializeWizard;
var
  PreviousMode: String;
  ParamMode: String;
begin
  DataModePage := CreateInputOptionPage(wpSelectDir,
    CustomMessage('DataModeTitle'), CustomMessage('DataModeDescription'),
    CustomMessage('DataModePrompt'), True, False);
  DataModePage.Add(CustomMessage('DataModeStandard'));
  DataModePage.Add(CustomMessage('DataModePortable'));
  if CompareText(ExpandConstant('{param:DATAMODE|}'), 'portable') = 0 then
    DataModePage.SelectedValueIndex := 1
  else if CompareText(GetPreviousData('DataMode', ''), 'portable') = 0 then
    DataModePage.SelectedValueIndex := 1
  else
    DataModePage.SelectedValueIndex := 0;

  RuntimeModePage := CreateInputOptionPage(DataModePage.ID,
    CustomMessage('RuntimeModeTitle'), CustomMessage('RuntimeModeDescription'),
    CustomMessage('RuntimeModePrompt'), True, False);
#if Flavor == "full"
  RuntimeModePage.Add(CustomMessage('RuntimeBundled'));
#else
  RuntimeModePage.Add(CustomMessage('RuntimeDownload'));
#endif
  RuntimeModePage.Add(CustomMessage('RuntimeArchive'));
  RuntimeModePage.Add(CustomMessage('RuntimeFolder'));
  RuntimeModePage.Add(CustomMessage('RuntimeSource'));
  RuntimeModePage.SelectedValueIndex := 0;
  PreviousMode := GetPreviousData('RuntimeMode', '');
  if PreviousMode <> '' then SelectRuntimeMode(PreviousMode);
  ParamMode := ExpandConstant('{param:RUNTIMEMODE|}');
  if ParamMode <> '' then SelectRuntimeMode(ParamMode);

  RuntimeArchivePage := CreateInputFilePage(RuntimeModePage.ID,
    CustomMessage('RuntimeArchiveTitle'), CustomMessage('RuntimeArchiveDescription'), '');
  RuntimeArchivePage.Add(CustomMessage('RuntimeArchivePrompt'), 'ZIP files|*.zip|All files|*.*', '.zip');
  RuntimeArchivePage.Values[0] := ExpandConstant('{param:RUNTIMEZIP|}');

  RuntimeFolderPage := CreateInputDirPage(RuntimeArchivePage.ID,
    CustomMessage('RuntimeFolderTitle'), CustomMessage('RuntimeFolderDescription'), '',
    False, SetupMessage(msgNewFolderName));
  RuntimeFolderPage.Add(CustomMessage('RuntimeFolderPrompt'));
  RuntimeFolderPage.Values[0] := ExpandConstant('{param:RUNTIMEFOLDER|}');

  SourceArchivePage := CreateInputFilePage(RuntimeFolderPage.ID,
    CustomMessage('SourceArchiveTitle'), CustomMessage('SourceArchiveDescription'), '');
  SourceArchivePage.Add(CustomMessage('SourceArchivePrompt'), 'ZIP files|*.zip|All files|*.*', '.zip');
  SourceArchivePage.Values[0] := ExpandConstant('{param:SOURCEZIP|}');

  DownloadPage := CreateDownloadPage(CustomMessage('DownloadTitle'), CustomMessage('DownloadDescription'), nil);
  DownloadPage.ShowBaseNameInsteadOfUrl := True;
  PreparationComplete := False;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
var
  Mode: String;
begin
  Mode := RuntimeModeKey;
  Result :=
    ((PageID = RuntimeArchivePage.ID) and (Mode <> 'archive')) or
    ((PageID = RuntimeFolderPage.ID) and (Mode <> 'folder')) or
    ((PageID = SourceArchivePage.ID) and (Mode <> 'source'));
end;

function ValidateRuntimeSelection: Boolean;
var
  Mode: String;
begin
  Mode := RuntimeModeKey;
  Result := True;
  if (Mode = 'archive') and not FileExists(RuntimeArchivePage.Values[0]) then begin
    MsgBox(CustomMessage('MissingRuntimeArchive'), mbError, MB_OK);
    Result := False;
  end else if (Mode = 'folder') and not DirExists(RuntimeFolderPage.Values[0]) then begin
    MsgBox(CustomMessage('MissingRuntimeFolder'), mbError, MB_OK);
    Result := False;
  end else if (Mode = 'source') and not FileExists(SourceArchivePage.Values[0]) then begin
    MsgBox(CustomMessage('MissingSourceArchive'), mbError, MB_OK);
    Result := False;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ErrorText: String;
begin
  Result := ValidateRuntimeSelection;
  if not Result then Exit;

  if (CurPageID = wpReady) and (RuntimeModeKey = 'download') then begin
    DownloadPage.Clear;
    DownloadPage.Add('{#RuntimeDownloadUrl}', '{#RuntimeAssetName}', '{#RuntimeSha256}');
    DownloadPage.Show;
    try
      try
        DownloadPage.Download;
        Result := True;
      except
        ErrorText := CustomMessage('DownloadFailed') + ': ' + GetExceptionMessage;
        SuppressibleMsgBox(ErrorText, mbCriticalError, MB_OK, IDOK);
        Result := False;
      end;
    finally
      DownloadPage.Hide;
    end;
  end;
end;

procedure RegisterPreviousData(PreviousDataKey: Integer);
begin
  SetPreviousData(PreviousDataKey, 'DataMode', DataModeKey);
  SetPreviousData(PreviousDataKey, 'RuntimeMode', RuntimeModeKey);
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
  MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
var
  S: String;
begin
  S := MemoDirInfo + NewLine + NewLine;
  S := S + CustomMessage('ReadyDataMode') + NewLine + Space;
  if DataModeKey = 'portable' then
    S := S + CustomMessage('ReadyDataPortable')
  else
    S := S + CustomMessage('ReadyDataStandard');
  S := S + NewLine + NewLine;
  S := S + CustomMessage('ReadyRuntimeMode') + NewLine + Space + RuntimeModeDisplay + NewLine + NewLine;
  if IsWebView2Installed then
    S := S + CustomMessage('WebViewPresent')
  else
    S := S + CustomMessage('WebViewMissing');
  if MemoTasksInfo <> '' then S := S + NewLine + NewLine + MemoTasksInfo;
  Result := S;
end;

function Quote(const Value: String): String;
begin
  Result := '"' + Value + '"';
end;

function RunPowerShell(const ScriptName, Arguments: String): Boolean;
var
  ResultCode: Integer;
  PowerShell: String;
  Parameters: String;
begin
  ExtractTemporaryFile(ScriptName);
  PowerShell := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
  Parameters := '-NoProfile -ExecutionPolicy Bypass -File ' + Quote(ExpandConstant('{tmp}\' + ScriptName)) + ' ' + Arguments;
  Result := Exec(PowerShell, Parameters, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function InstallWebView2: Boolean;
var
  InstallerName: String;
  ResultCode: Integer;
begin
  if IsWebView2Installed then begin
    Result := True;
    Exit;
  end;
#if Flavor == "full"
  InstallerName := 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe';
#else
  InstallerName := 'MicrosoftEdgeWebview2Setup.exe';
#endif
  ExtractTemporaryFile(InstallerName);
  Result := Exec(ExpandConstant('{tmp}\' + InstallerName), '/silent /install', '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode) and ((ResultCode = 0) or (ResultCode = 3010));
end;

function InstallSelectedRuntime: Boolean;
var
  Mode: String;
  InputPath: String;
  Arguments: String;
begin
  Mode := RuntimeModeKey;
  if Mode = 'bundled' then begin
    ExtractTemporaryFile(ExtractFileName('{#RuntimeArchive}'));
    InputPath := ExpandConstant('{tmp}\' + ExtractFileName('{#RuntimeArchive}'));
    Mode := 'archive';
  end else if Mode = 'download' then begin
    InputPath := ExpandConstant('{tmp}\{#RuntimeAssetName}');
    Mode := 'archive';
  end else if Mode = 'archive' then
    InputPath := RuntimeArchivePage.Values[0]
  else if Mode = 'folder' then
    InputPath := RuntimeFolderPage.Values[0]
  else
    InputPath := SourceArchivePage.Values[0];

  Arguments := '-Mode ' + Mode +
    ' -Destination ' + Quote(ExpandConstant('{app}\runtime')) +
    ' -InputPath ' + Quote(InputPath);
  if (Mode = 'archive') and ((RuntimeModeKey = 'bundled') or (RuntimeModeKey = 'download')) then
    Arguments := Arguments + ' -ExpectedSha256 {#RuntimeSha256}';
  Result := RunPowerShell('install-runtime.ps1', Arguments);
end;

function SeedFirstRunConfig: Boolean;
var
  Language: String;
  Arguments: String;
begin
  if ActiveLanguage = 'chinesesimp' then Language := 'zh-CN' else Language := 'en-US';
  Arguments := '-Language ' + Language + ' -AppDirectory ' + Quote(ExpandConstant('{app}'));
  if DataModeKey = 'portable' then Arguments := Arguments + ' -Portable';
  Result := RunPowerShell('seed-config.ps1', Arguments);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  NeedsRestart := False;
  if PreparationComplete then Exit;

  if not InstallWebView2 then begin
    Result := CustomMessage('WebViewInstallFailed');
    Exit;
  end;
  if not InstallSelectedRuntime then begin
    Result := CustomMessage('RuntimeInstallFailed');
    Exit;
  end;

  if DataModeKey = 'portable' then
    SaveStringToFile(ExpandConstant('{app}\portable.mode'), '', False)
  else if FileExists(ExpandConstant('{app}\portable.mode')) then
    DeleteFile(ExpandConstant('{app}\portable.mode'));

  if not SeedFirstRunConfig then begin
    Result := CustomMessage('ConfigSeedFailed');
    Exit;
  end;
  PreparationComplete := True;
end;
