# DeepSeek Harness Windows 桌面版

[English](README.md) | 中文

本目录负责非官方 Windows 桌面外壳及其发布流程。桌面窗口通过 Microsoft Edge WebView2 嵌入上游 Web UI，打包后的 DeepSeek Harness 服务在本机运行。

## 安装

请从 [GitHub Releases 页面](https://github.com/Iraryi/deepseek-harness-desktop/releases)下载合适的资产。

- `Full Setup` 是推荐的离线可用安装包，内置应用 Runtime、私有 Node.js Runtime 和微软 WebView2 离线安装器。
- `Lite Setup` 是小型安装包，会在线下载并校验应用 Runtime，也能导入从其他电脑下载的 Runtime ZIP。
- `Portable ZIP` 不注册安装信息，设置与用户数据保存在程序旁边；电脑必须已经具备 WebView2。
- `Runtime ZIP` 是可独立校验的服务载荷，供安装程序导入和修复模式使用。

正常使用 Full、Lite 或便携版不需要安装系统级 Node.js。打包 Runtime 包含 `tools\node\node.exe`，启动器会优先使用它，而不是系统 Node.js。

## 安装程序参考

安装程序依次提供语言、数据位置和 Runtime 来源选择。应用首次启动时会先打开 CONFIG，再进入主窗口，用户可设置语言、分辨率、启动形式、加载画面、托盘行为和可选的浏览器扩展目录。

Runtime 来源可以是 Full 包内置载荷、经过校验的 GitHub 下载、本地 Runtime ZIP、已有 Runtime 文件夹或 GitHub 源码 ZIP。源码 ZIP 模式是高级本地构建路径，要求 Node.js 22.19 或更高版本和 `pnpm`；安装程序会检测这些工具，但不会在系统中全局安装它们。

安装程序会检测 WebView2，并在缺失时安装。Runtime 替换使用 staging 和 backup 目录，因此校验或复制失败时会保留原 Runtime。依赖或 Runtime 准备失败会在应用文件安装前停止 Setup，并返回非零退出码。

标准数据位于 `%LOCALAPPDATA%\DeepSeekHarness`。便携数据位于 `dsh.exe` 旁边的 `data`。升级和卸载都会保留用户创建的数据；卸载会移除打包 Runtime 和启动器文件。

## 构建参考

在 Windows x64 源码目录中准备 Node.js 24、`pnpm`、.NET Framework 编译器和 Inno Setup 6，然后运行：

```powershell
pnpm install --frozen-lockfile
powershell -NoProfile -ExecutionPolicy Bypass -File windows/release/build.ps1
```

该脚本会构建 WebView2 启动器、构建闭合的工作区 Runtime、按需下载并校验微软签名的 WebView2 安装器、编译 Full 与 Lite Setup、运行安装 smoke 测试、创建便携 ZIP 和带版本号的 Runtime ZIP，并在 `windows/release/dist` 写入 `release-manifest.json` 与 `SHA256SUMS.txt`。

使用 `windows/release/download.ps1` 可以下载并校验已发布的 Full、Lite、便携或 Runtime 资产。`windows/setup/install-runtime.ps1` 负责本地压缩包、文件夹和源码安装，并以原子方式替换 Runtime。

## 源码布局

- `launcher` 包含 WinForms/WebView2 主程序与 CONFIG 程序。
- `runtime` 定义并构建传递闭合的服务载荷及其私有 Node.js Runtime。
- `setup` 包含 Inno Setup 工程、Runtime 安装脚本、首次运行配置写入和安装 smoke 测试。
- `release` 负责组合最终资产、校验和、Release 说明和带校验的下载器。
