# ClaudeConsole

Windows 桌面应用 — 一站式管理 [Claude Code](https://claude.ai/code) 配置：API 提供商、模型、MCP 服务器、技能、配置文件。

> 🖥️ 本项目是 macOS 版 [ClaudeCodePanel](https://github.com/RRRadiant/ClaudeCodePanel) 的 Windows 移植版。

## ✨ 功能

| 面板 | 说明 |
|------|------|
| **概览** | Claude Code 安装状态、API 连接、模型数、MCP 服务器、技能总览 |
| **API 配置** | 管理 API 密钥（存储在 Windows 凭据管理器），支持 Anthropic / OpenAI / DeepSeek / 自定义，连接测试，模型检测 |
| **配置文件** | 浏览编辑 `~/.claude/` JSON 配置文件，mtime 冲突检测 |
| **MCP 服务器** | 增删改 MCP 服务器，连接测试，本地别名管理 |
| **技能** | 浏览 GitHub Marketplace，安装 / 卸载技能，启用 / 禁用 |
| **安装器** | 一键安装 / 卸载 Claude Code CLI（npm / winget） |
| **环境检测** | 检测 Node.js、npm、Git 安装状态及版本 |

### 🎨 界面
- Windows 11 Mica 毛玻璃效果（Win10 降级为深色背景）
- 暗色主题、自定义标题栏
- 侧边栏导航 + 内容区切换

### 🔄 自动更新
- 启动时自动检测 GitHub Releases 新版本
- 侧边栏底部手动「检查更新」按钮
- 检测到新版本 → 顶部蓝色横幅，一键跳转下载

## 📦 下载

前往 [Releases](https://github.com/Lyxxxx718/ClaudeConsole/releases) 下载最新版 `ClaudeConsole.exe`，双击即可运行，无需安装 .NET 运行时。

## 🔧 从源码构建

```powershell
git clone https://github.com/Lyxxxx718/ClaudeConsole.git
cd ClaudeConsole\ClaudeCodePanel.Windows
dotnet restore
dotnet build -c Release
dotnet run -c Release --project src/ClaudeCodePanel.Windows
```

或在 Visual Studio 2022 中打开 `ClaudeCodePanel.Windows.sln` 按 F5 运行。

## 🧱 技术栈

| 层级 | 技术 |
|------|------|
| UI | WPF (XAML) |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| HTTP | System.Net.Http |
| JSON | System.Text.Json |
| 凭据存储 | Windows Credential Manager (advapi32.dll) |
| 文件监控 | System.IO.FileSystemWatcher |

## 📁 项目结构

```
src/ClaudeCodePanel.Windows/
├── App.xaml(.cs)              # 入口、DI 容器、Mica 设置
├── Models/                    # APIProvider, DashboardSummary, MCPServerConfig, SkillItem, UpdateInfo
├── Services/                  # ConfigFileService, CredentialService, MCPService, SyncService,
│                              # SkillRepositoryService, InstallerService, EnvironmentService,
│                              # FileWatcherService, UpdateService
├── ViewModels/                # MainViewModel + 7 个面板 ViewModel
├── Views/
│   ├── MainWindow.xaml        # 主窗口（自定义标题栏 + 侧边栏 + 内容区）
│   ├── Sidebar/               # 侧边栏导航 + 版本/更新状态
│   ├── Dashboard/             # 概览面板
│   ├── API/                   # API 配置面板
│   ├── Config/                # 配置文件编辑器
│   ├── MCP/                   # MCP 服务器管理
│   ├── Skills/                # 技能管理
│   ├── Installer/             # CLI 安装器
│   ├── EnvCheck/              # 环境检测
│   └── Shared/                # GlassCard, GlassButton, StatusIndicator 等 8 个公用控件
├── Converters/                # XAML 值转换器
├── Helpers/                   # MCPDisplayNameStore, Windows11Interop
└── Resources/Themes/          # DarkTheme.xaml
```

## 🔄 macOS 对应关系

| macOS (SwiftUI) | Windows (WPF) |
|---|---|
| `@Observable` 宏 | `[ObservableProperty]` 源生成器 |
| macOS Keychain | Windows Credential Manager |
| `~/.claude/` | `%USERPROFILE%/.claude/` |
| SF Symbols | Segoe MDL2 Assets |
| `.glassBackgroundEffect()` | Mica 背景 + 半透明画刷 |

## 📄 许可

与原项目 [ClaudeCodePanel](https://github.com/RRRadiant/ClaudeCodePanel) 保持一致。
