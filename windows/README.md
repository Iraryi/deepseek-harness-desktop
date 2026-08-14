# DeepSeek Harness Desktop for Windows

English | [中文](README.zh.md)

This subtree owns the unofficial Windows desktop shell and its release pipeline. The desktop window embeds the upstream Web UI through Microsoft Edge WebView2 while the packaged DeepSeek Harness service runs locally.

## Install

Download the preferred asset from the [GitHub Releases page](https://github.com/Iraryi/deepseek-harness-desktop/releases).

- `Full Setup` is the recommended offline-capable installer. It includes the application Runtime, a private Node.js runtime, and the Microsoft WebView2 offline installer.
- `Lite Setup` is a small installer that downloads and verifies the application Runtime. It can also import a Runtime ZIP downloaded on another computer.
- `Portable ZIP` runs without registration and stores configuration and user data beside the executable. The computer must already provide WebView2.
- `Runtime ZIP` is the independently verifiable service payload used by Setup import and repair modes.

Normal Full, Lite, and portable use does not require a system Node.js installation. The packaged Runtime contains `tools\node\node.exe` and the launcher prefers it over any system Node.js.

## Setup reference

Setup starts with language selection, data placement, and Runtime source selection. The first application launch opens CONFIG before the main window so the user can choose language, resolution, launch mode, loading presentation, tray behavior, and optional browser-extension directories.

The Runtime source can be the Full package payload, a verified GitHub download, a local Runtime ZIP, an existing Runtime folder, or a GitHub source ZIP. Source ZIP mode is an advanced local build path and requires Node.js 22.19 or newer plus `pnpm`; Setup detects these tools but does not install them globally.

Setup detects WebView2 and installs it when absent. Runtime replacement uses staging and backup directories so a validation or copy failure retains the previous Runtime. A failed prerequisite or Runtime preparation stops Setup with a nonzero exit code before application files are installed.

Standard data lives under `%LOCALAPPDATA%\DeepSeekHarness`. Portable data lives in `data` beside `dsh.exe`. Upgrade and uninstall preserve user-created data; uninstall removes the packaged Runtime and launcher files.

## Build reference

Run the release pipeline from a Windows x64 checkout with Node.js 24, `pnpm`, the .NET Framework compiler, and Inno Setup 6:

```powershell
pnpm install --frozen-lockfile
powershell -NoProfile -ExecutionPolicy Bypass -File windows/release/build.ps1
```

The script builds the WebView2 launcher, builds the closed workspace Runtime, downloads Microsoft-signed WebView2 installers when missing, compiles Full and Lite Setup, runs the installation smoke test, creates the portable and versioned Runtime ZIP files, and writes `release-manifest.json` plus `SHA256SUMS.txt` under `windows/release/dist`.

Use `windows/release/download.ps1` to download and verify a published Full, Lite, portable, or Runtime asset. `windows/setup/install-runtime.ps1` owns local archive, folder, and source installation with atomic Runtime replacement.

## Source layout

- `launcher` contains the WinForms/WebView2 main application and CONFIG application.
- `runtime` defines and builds the transitive packaged service closure with its private Node.js runtime.
- `setup` contains the Inno Setup project, Runtime installer, first-run configuration seeding, and installation smoke test.
- `release` composes final assets, checksums, release notes, and the verified downloader.
