# Agent Note: Windows 桌面发行版

Status: implemented

[English](2026-08-14-windows-desktop-distribution.md) | 中文

## 问题

由浏览器承载的 Web UI 并不是完整的 Windows 应用体验。面向用户的桌面发行版需要原生窗口、配置程序、可预测的本地服务启动、支持离线的安装、便携使用方式，以及无需用户自行组装 Node.js 工作区的发布格式。

服务与 UI 仍然是插件化的源码包，而不是单体可执行文件。如果没有明确的 Runtime 边界，打包过程只能把可变的源码目录复制进程序目录，或依赖系统全局安装的 Node.js 和包管理器状态。

## 决策

Windows 产品分为小型原生启动层和带版本的应用 Runtime。`windows/launcher` 将 `dsh.exe` 与 `dsh-config.exe` 构建为 WinForms 应用；主程序使用打包 Runtime 启动本地服务，并在 WebView2 内呈现其 Web UI。CONFIG 程序负责桌面显示设置和首次运行配置，同时不改变上游 Web UI 的插件模型。

`windows/runtime` 定义专用工作区部署根，校验其传递闭合的工作区依赖，实体化链接，内置私有 Windows Node.js 可执行文件，并生成清单与 Runtime ZIP。启动器会优先选择该私有 Node.js，而不是任何系统安装。在桌面约定不变时，插件变化继续通过打包后的 harness CLI 与 Runtime 内容交付，不要求重新编译原生启动器。

`windows/setup` 提供 Full 与 Lite 两种 Inno Setup 安装包。Full 内置 Runtime ZIP 和带微软签名的 WebView2 离线安装器；Lite 下载带版本的 Runtime，并使用 WebView2 引导安装器。两种安装包都能导入本地 Runtime ZIP、复制已有 Runtime 文件夹，或在电脑已有 Node.js 22.19 或更高版本与 `pnpm` 时构建源码 ZIP。Setup 会校验 Runtime 清单和内置 Node.js、拒绝链接、通过 staging 与 backup 目录替换 Runtime、保留用户数据，并在依赖准备失败时于应用文件安装前停止。

标准数据位于 `%LOCALAPPDATA%\DeepSeekHarness`；便携数据位于可执行文件旁边的 `data`。首次使用会先打开 CONFIG，沿用 Setup 选择的语言，但保持首次配置未完成。升级和卸载都会保留标准与便携模式中的用户创建数据。

`windows/release` 组合 Full Setup、Lite Setup、便携 ZIP、带版本的 Runtime ZIP、校验和、发布清单、Release 说明和带校验的下载器。GitHub Actions 在原生 Windows 上运行同一发布脚本并完成 smoke 测试，然后为匹配版本的标签发布这些文件。生成的二进制、下载的供应商缓存、解压后的 Runtime 目录和 smoke 测试目录都不会进入 Git 历史。

## 考虑过的替代方案

**在用户浏览器中打开本地 URL。** 这种方式能保持启动器小巧，但会继续暴露浏览器边框和浏览器进程行为，也无法提供独立的桌面显示与托盘约定。

**把整个服务塞进原生可执行文件。** 单文件会掩盖插件与工作区部署模型，使 Node 原生依赖更难处理，并导致普通服务或 Web UI 变化也必须重建启动器。

**要求全局安装 Node.js 与 pnpm。** 这能减小发布体积，但会让普通用户负责兼容的工具版本和可变的全局状态。只有源码 ZIP 构建这一明确的高级模式继续要求全局工具。

**只发布一个在线安装包。** 小型在线包适合可靠网络，但无法覆盖离线安装和手工转移。Full、Lite、本地 Runtime 导入和便携资产分别覆盖这些交付条件。

**把所有用户数据放进安装目录。** 这能简化路径查找，但会让标准升级和卸载变得不安全，也不符合 Windows 的每用户数据约定。便携模式通过明确选择使用相邻数据目录。

## 后果

Windows 用户获得原生应用入口、首次配置流程、离线与在线安装选择，以及不依赖系统 Node.js 的 Runtime。发行版体积更大，因为 Full Setup 与便携资产包含服务闭包和私有 Node.js，Full Setup 还包含 WebView2 离线安装器。

发布正确性依赖 Runtime 清单、工作区闭包校验、下载 WebView2 安装器的微软签名校验、安装器 smoke 覆盖和公开的 SHA-256 文件。源码 ZIP 模式仍然较慢并依赖已有构建工具，而正常安装保持自包含。
