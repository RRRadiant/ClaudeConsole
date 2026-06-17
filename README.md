# Claude Code Panel for Windows

Windows desktop application for managing [Claude Code](https://claude.ai/code) configuration — API providers, models, MCP servers, skills, and config files.

Ported from the [macOS SwiftUI version](../ClaudeCodePanel).

## Features

- **Dashboard** — Overview of Claude Code installation, enabled models, skills, and MCP servers
- **API Configuration** — Manage API keys (stored in Windows Credential Manager), test connections, detect available models
- **Config Editor** — Browse and edit `~/.claude/` JSON config files with conflict detection
- **MCP Servers** — Add, edit, delete MCP servers with connection testing and local display-name renaming
- **Skills** — Browse GitHub marketplace, install/uninstall skills, toggle enabled state

## Requirements

- Windows 10 version 2004+ or Windows 11
- .NET 9.0 SDK or Runtime
- [Claude Code CLI](https://docs.anthropic.com/en/docs/claude-code) (optional — for dashboard metrics)

## Build from Source

```powershell
# Clone or open the solution
cd ClaudeCodePanel.Windows
dotnet restore
dotnet build -c Release

# Run
dotnet run -c Release --project src/ClaudeCodePanel.Windows
```

## Open in Visual Studio

1. Open `ClaudeCodePanel.Windows.sln` in Visual Studio 2022 (17.8+)
2. Set `ClaudeCodePanel.Windows` as startup project
3. Build and run (F5)

## Architecture

| Layer | Technology |
|-------|-----------|
| UI | WPF (XAML) |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| HTTP | System.Net.Http.HttpClient |
| JSON | System.Text.Json |
| Credential Storage | Windows Credential Manager (advapi32.dll) |
| Process Management | System.Diagnostics.Process |
| File Watching | System.IO.FileSystemWatcher |
| Design | Windows 11 Mica backdrop + dark theme |

## Project Structure

```
src/ClaudeCodePanel.Windows/
├── App.xaml(.cs)           # Application entry, DI container, Mica setup
├── Models/                 # APIProvider, ClaudeConfig, DashboardSummary, MCPServerConfig, SkillItem
├── Services/               # ConfigFileService, CredentialService, MCPService, FileWatcherService, SyncService, SkillRepositoryService
├── ViewModels/             # MVVM ViewModels (Dashboard, APIConfig, ConfigEditor, MCPManager, SkillManager, Main)
├── Views/
│   ├── MainWindow.xaml     # Window shell with custom title bar + sidebar navigation
│   ├── Sidebar/            # SidebarView — 5 panel navigation
│   ├── Dashboard/          # DashboardView — metrics overview
│   ├── API/                # APIConfigView, APIKeyInputView
│   ├── Config/             # ConfigEditorView
│   ├── MCP/                # MCPServerListView, MCPServerCard, MCPServerEditorView
│   ├── Skills/             # SkillsListView, SkillCard, SkillInstallDialog
│   └── Shared/             # GlassCard, GlassButton, GlassTextField, StatusIndicator, Badge, SearchField, AsyncButton, UtilityViews
├── Converters/             # XAML value converters
├── Helpers/                # MCPDisplayNameStore, Windows11Interop
└── Resources/Themes/       # DarkTheme.xaml
```

## Port Notes

| macOS (SwiftUI) | Windows (WPF) |
|---|---|
| `@Observable` macro | `[ObservableProperty]` source generator |
| macOS Keychain | Windows Credential Manager |
| `~/.claude/` | `%USERPROFILE%/.claude/` |
| SF Symbols | Segoe MDL2 Assets (glyphs) |
| `.glassBackgroundEffect()` | Mica backdrop + semi-transparent brushes |
| DMG install banner | Removed (not applicable) |

## License

Same as the macOS ClaudeCodePanel project.
