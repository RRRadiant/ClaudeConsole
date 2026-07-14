# ClaudeConsole

A Windows desktop app for managing [Claude Code](https://claude.ai/code) configuration — API providers, models, MCP servers, skills, and config files.

> 🖥️ Windows port of the macOS app [ClaudeCodePanel](https://github.com/RRRadiant/ClaudeCodePanel).

## ✨ Features

| Panel | Description |
|-------|-------------|
| **Dashboard** | Claude Code status, API connection, model count, MCP servers, skills overview |
| **API Config** | Manage API keys (saved to Windows Credential Manager and mirrored into Claude Code config), support for Anthropic / OpenAI / DeepSeek / Custom, connection testing, model detection |
| **Config Editor** | Browse and edit `~/.claude/` JSON config files with mtime conflict detection |
| **MCP Servers** | Add, edit, delete MCP servers with connection testing and local display-name aliases |
| **Skills** | Browse GitHub Marketplace, install / uninstall skills, toggle enabled state |
| **Installer** | One-click install / uninstall Claude Code CLI (npm / winget) |
| **Env Check** | Detect Node.js, npm, and Git installation status and versions |

### 🎨 UI
- Windows 11 Mica backdrop (falls back to dark solid background on Windows 10)
- Dark theme, custom title bar
- Sidebar navigation with content area swapping

### 🔄 Auto Update
- Checks GitHub Releases for new versions on startup
- Manual "Check for Updates" button in the sidebar footer
- Update notification banner appears when a newer version is found — one click to download

## 📦 Download

Go to [Releases](https://github.com/RRRadiant/ClaudeConsole/releases) and download the latest `ClaudeConsole.exe`. Double-click to run — no .NET runtime installation required.

## 🔧 Build from Source

```powershell
git clone https://github.com/RRRadiant/ClaudeConsole.git
cd ClaudeConsole\ClaudeCodePanel.Windows
dotnet restore
dotnet build -c Release
dotnet run -c Release --project src/ClaudeCodePanel.Windows
```

Or open `ClaudeCodePanel.Windows.sln` in Visual Studio 2022 and press F5.

## 🧱 Tech Stack

| Layer | Technology |
|-------|-----------|
| UI | WPF (XAML) |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| HTTP | System.Net.Http |
| JSON | System.Text.Json |
| Credential Storage | Windows Credential Manager (advapi32.dll) |
| File Watching | System.IO.FileSystemWatcher |

## 📁 Project Structure

```
src/ClaudeCodePanel.Windows/
├── App.xaml(.cs)              # Entry point, DI container, Mica setup
├── Models/                    # APIProvider, DashboardSummary, MCPServerConfig, SkillItem, UpdateInfo
├── Services/                  # ConfigFileService, CredentialService, MCPService, SyncService,
│                              # SkillRepositoryService, InstallerService, EnvironmentService,
│                              # FileWatcherService, UpdateService
├── ViewModels/                # MainViewModel + 7 panel ViewModels
├── Views/
│   ├── MainWindow.xaml        # Main window (custom title bar + sidebar + content area)
│   ├── Sidebar/               # Sidebar navigation + version/update status
│   ├── Dashboard/             # Dashboard panel
│   ├── API/                   # API config panel
│   ├── Config/                # Config file editor
│   ├── MCP/                   # MCP server manager
│   ├── Skills/                # Skill manager
│   ├── Installer/             # CLI installer
│   ├── EnvCheck/              # Environment check
│   └── Shared/                # 8 shared controls (GlassCard, GlassButton, StatusIndicator, etc.)
├── Converters/                # XAML value converters
├── Helpers/                   # MCPDisplayNameStore, Windows11Interop
└── Resources/Themes/          # DarkTheme.xaml
```

## 🔄 macOS Equivalents

| macOS (SwiftUI) | Windows (WPF) |
|---|---|
| `@Observable` macro | `[ObservableProperty]` source generator |
| macOS Keychain | Windows Credential Manager |
| `~/.claude/` | `%USERPROFILE%/.claude/` |
| SF Symbols | Segoe MDL2 Assets |
| `.glassBackgroundEffect()` | Mica backdrop + semi-transparent brushes |

## 📄 License

Same as the original [ClaudeCodePanel](https://github.com/RRRadiant/ClaudeCodePanel) project.
