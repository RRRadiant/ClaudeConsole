# ClaudeCodePanel.Windows — Handoff Document

## What This Is

A **Windows port** (C# + .NET 9 + WPF) of the macOS SwiftUI app "ClaudeCodePanel" — a configuration panel manager for Claude Code. All 5 panels: Dashboard, API Config, Config Editor, MCP Manager, Skill Manager.

**Tech:** WPF, CommunityToolkit.Mvvm (source-gen MVVM), Microsoft.Extensions.DependencyInjection, Windows Credential Manager (P/Invoke), Windows 11 Mica backdrop.

## Quick Start (on Windows)

```powershell
cd ClaudeCodePanel.Windows
dotnet restore
dotnet build -c Release
dotnet run -c Release --project src/ClaudeCodePanel.Windows
```

Requires: **.NET 9.0 SDK** (https://dotnet.microsoft.com/download/dotnet/9.0)

---

## Build Status

**Current:** 0 errors, 0 warnings — clean build on `dotnet build -c Release`.

### Resolved Issues

| Issue | Status |
|-------|--------|
| MC3093 namescope conflict (GlassCard UserControl) | ✅ Fixed — converted to ContentControl with ControlTemplate |
| Audit bug #1: MCPService stderr capture | ✅ Fixed — async Task.Run capture |
| Audit bug #2: CancellationTokenRegistration leak | ✅ Fixed — using var ctr |
| Audit bug #3: FileWatcher disposed check | ✅ Fixed — lock check in OnDebounceElapsed |
| Audit bug #4: Non-atomic file write | ✅ Fixed — File.Move with overwrite |
| Audit bug #5: Hardcoded Unix path | ✅ Fixed — platform detection (cmd.exe vs /usr/bin/env) |
| 37 build warnings (CS8622, CS8602, MVVMTK0034, CS0169, CS0414) | ✅ All eliminated |
| API config mismatch with local config | ✅ Fixed — SyncService reads env.* keys |
| Provider selection not working | ✅ Fixed — x:Name field access instead of Loaded event |
| App window not showing | ✅ Fixed — MainWindow.Show() added to OnStartup |
| Mica crash on startup | ✅ Fixed — moved to SourceInitialized |

---

## Project File Map

```
ClaudeCodePanel.Windows/
├── ClaudeCodePanel.Windows.sln
├── HANDOFF.md
├── README.md
├── .gitignore
└── src/ClaudeCodePanel.Windows/
    ├── ClaudeCodePanel.Windows.csproj  ← net9.0-windows, 3 NuGet packages
    ├── App.xaml / App.xaml.cs          ← DI container, singleton services, lifecycle
    ├── Models/
    │   ├── APIProvider.cs              ← Anthropic/OpenAI/DeepSeek/Custom enum
    │   ├── ClaudeConfig.cs             ← Full Claude config model
    │   ├── DashboardSummary.cs         ← Dashboard data model
    │   ├── IndicatorStatus.cs          ← Running/Stopped/Error enum
    │   ├── MCPServerConfig.cs          ← MCP server model + connection result
    │   └── SkillItem.cs               ← Skill model
    ├── Services/
    │   ├── ConfigFileService.cs       ← File I/O for ~/.claude/*.json
    │   ├── CredentialService.cs       ← Windows Credential Manager P/Invoke
    │   ├── FileWatcherService.cs      ← Debounced file watcher
    │   ├── MCPService.cs             ← MCP connection testing
    │   ├── SkillRepositoryService.cs  ← GitHub marketplace fetch + install
    │   └── SyncService.cs            ← Cross-process sync coordination
    ├── ViewModels/
    │   ├── MainViewModel.cs           ← Navigation hub
    │   ├── DashboardViewModel.cs      ← Loads summary from config files
    │   ├── APIConfigViewModel.cs      ← Provider config, key mgmt, model detection
    │   ├── ConfigEditorViewModel.cs   ← File tabs, edit, save with conflict detection
    │   ├── MCPManagerViewModel.cs     ← Server CRUD, connection testing
    │   └── SkillManagerViewModel.cs   ← Installed + marketplace skill management
    ├── Views/
    │   ├── MainWindow.xaml(.cs)       ← Custom title bar, Mica backdrop, navigation
    │   ├── Sidebar/SidebarView.xaml(.cs)
    │   ├── Dashboard/DashboardView.xaml(.cs)
    │   ├── API/APIConfigView.xaml(.cs), APIKeyInputView.xaml(.cs)
    │   ├── Config/ConfigEditorView.xaml(.cs)
    │   ├── MCP/MCPServerListView.xaml(.cs), MCPServerCard.xaml(.cs), MCPServerEditorView.xaml(.cs)
    │   ├── Skills/SkillsListView.xaml(.cs), SkillCard.xaml(.cs), SkillInstallDialog.xaml(.cs)
    │   └── Shared/  (8 controls)
    │       ├── GlassCard.xaml(.cs)        ← ContentControl with ControlTemplate
    │       ├── GlassButton.xaml(.cs)
    │       ├── GlassTextField.xaml(.cs)
    │       ├── StatusIndicator.xaml(.cs)
    │       ├── Badge.xaml(.cs)
    │       ├── AsyncButton.xaml(.cs)
    │       ├── SearchField.xaml(.cs)
    │       └── UtilityViews.xaml(.cs)     ← GlassDivider, EmptyState, SectionHeader
    ├── Converters/ValueConverters.cs
    ├── Helpers/
    │   ├── MCPDisplayNameStore.cs     ← Rename persistence for MCP servers
    │   └── Windows11Interop.cs        ← Mica P/Invoke (DwmSetWindowAttribute)
    └── Resources/Themes/DarkTheme.xaml ← Global styles + DataTemplates
```

---

## Architecture Notes

- **Singleton services** use `private constructor` + `public static Instance { get; } = new()` pattern, registered via `services.AddSingleton(Service.Instance)`
- **ViewModels** use CommunityToolkit.Mvvm `[ObservableProperty]` and `[RelayCommand]` source generators
- **API config** reads Claude Code's `env.ANTHROPIC_*` keys from settings.json + settings.local.json via SyncService
- **Config writes** use read-merge-write pattern with atomic `File.Move(temp, path, overwrite: true)`
- **GlassCard** is a ContentControl (not UserControl) to avoid WPF namescope conflicts with `x:Name`

---

## Design Tokens

| Token | Value |
|-------|-------|
| Window background | `#1C1C1E` |
| Sidebar background | `#1A1A1D` |
| Content background | `#1A1A1A` |
| GlassCard fill | `#CC1A1A1A` (80% opacity) |
| GlassCard border | `rgba(255,255,255,0.06)` → hover `0.12` |
| Accent | `#007AFF` |
| Success (green) | `#30D158` |
| Error (red) | `#FF453A` |
| Corner radius | 14 (cards), 8 (inputs, buttons) |
| Icon font | Segoe MDL2 Assets |
| Config path | `%USERPROFILE%\.claude\` |

---

## Publishing

```powershell
# Framework-dependent (smaller, requires .NET 9 runtime)
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/

# Self-contained (larger, no runtime needed)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish-self-contained/
```
