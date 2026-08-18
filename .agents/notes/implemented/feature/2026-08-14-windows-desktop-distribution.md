# Agent Note: Windows desktop distribution

Status: implemented

English | [中文](2026-08-14-windows-desktop-distribution.zh.md)

## Problem

The browser-served Web UI is not a complete Windows application experience. A user-facing desktop distribution needs a native window, a configuration program, predictable local service startup, offline-capable installation, portable use, and a release format that does not require users to assemble Node.js workspaces.

The service and UI remain plugin-oriented source packages rather than a monolithic executable. Packaging them without a defined Runtime boundary would either copy a mutable source checkout into the application directory or depend on globally installed Node.js and package-manager state.

## Decision

The Windows product is split into a small native launcher layer and a versioned application Runtime. `windows/launcher` builds `dsh.exe` and `dsh-config.exe` as WinForms applications; the main process starts the local service with the packaged Runtime and renders its Web UI inside WebView2. The CONFIG application owns desktop presentation settings and first-run configuration without changing the upstream Web UI's plugin model.

`windows/runtime` defines a dedicated workspace deploy root, verifies its transitive workspace closure, materializes links, bundles a private Windows Node.js executable, and emits a manifest plus Runtime ZIP. The launcher selects this private Node.js before any system installation. Plugin changes continue through the packaged harness CLI and Runtime content rather than native launcher recompilation when the desktop contract is unchanged.

`windows/setup` provides Full and Lite Inno Setup packages. Full includes the Runtime ZIP and the Microsoft-signed WebView2 offline installer; Lite downloads the versioned Runtime and uses the WebView2 bootstrapper. Both packages can import a local Runtime ZIP, copy an existing Runtime folder, or build a source ZIP when Node.js 22.19 or newer and `pnpm` already exist. Setup validates Runtime manifests and bundled Node.js, rejects links, replaces Runtime through staging and backup directories, preserves user data, and stops before application file installation when prerequisite preparation fails.

Standard data belongs under `%LOCALAPPDATA%\DeepSeekHarness`; portable data belongs beside the executables under `data`. First use opens CONFIG with the Setup language seeded but unfinished. Upgrade and uninstall preserve both standard and portable user-created data.

`windows/release` composes Full Setup, Lite Setup, a portable ZIP, a versioned Runtime ZIP, checksums, a release manifest, release notes, and a verified downloader. GitHub Actions builds and smoke-tests the same release script on native Windows and publishes those files for matching version tags. Generated binaries, downloaded vendor caches, extracted Runtime trees, and smoke-test directories stay outside Git history.

## Alternatives considered

**Open the local URL in the user's browser.** This keeps the launcher small but retains browser chrome, browser process behavior, and no independent desktop presentation or tray contract.

**Bundle the entire service into the native executable.** A single binary would obscure the plugin and workspace deployment model, complicate Node-native dependencies, and require launcher rebuilds for ordinary service and Web UI changes.

**Require a global Node.js and pnpm installation.** This reduces release size but makes normal users responsible for compatible tool versions and mutable global state. Global tools remain an explicit advanced requirement only for source ZIP builds.

**Ship only one online installer.** A small online package is convenient on reliable networks but does not serve offline installation or manual transfer. Full, Lite, local Runtime import, and portable assets cover those distinct delivery conditions.

**Store all user data inside the installation directory.** This simplifies path discovery but makes standard upgrades and uninstall behavior unsafe and conflicts with per-user Windows conventions. Portable mode opts into adjacent data explicitly.

## Consequences

Windows users receive a native application entry, a first-run configuration flow, offline and online installation choices, and a Runtime that does not depend on system Node.js. The distribution is larger because Full Setup and portable assets carry the service closure, private Node.js, and in Full Setup the WebView2 offline installer.

Release correctness depends on the Runtime manifest, workspace-closure verification, Microsoft signature checks for downloaded WebView2 installers, installer smoke coverage, and published SHA-256 files. Source ZIP mode remains slower and depends on existing build tools, while normal installation remains self-contained.
