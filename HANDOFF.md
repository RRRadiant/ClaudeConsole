# ClaudeConsole — Handoff Document

## What This Is

A **Windows desktop app** (C# + .NET 9 + WPF) for managing Claude Code configuration.
Windows port of the macOS SwiftUI app [ClaudeCodePanel](https://github.com/RRRadiant/ClaudeCodePanel).

**7 panels:** Dashboard, API Config, Config Editor, MCP Manager, Skill Manager, Installer, Env Check.

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

### Completed Optimizations (22 items from audit)

| Category | Changes |
|----------|---------|
| 🔴 Bug fixes (3) | Marketplace install implemented (shallow clone), removed MCPService StopAll dead code, CredentialService Exists no longer uses exception-as-control-flow (TryRead) |
| 🟠 Performance (4) | JSON EnumerateObject instead of GetRawText+Deserialize (9 call sites), SkillManager filtered properties cached, MCPDisplayNameStore write debounce (2s), dead code removal |
| 🟡 Architecture (5) | MainWindow DI constructor injection, FileWatcherService.OnChange Action→event, PersistToClaudeJSONAsync→sync void, deleted unused ClaudeConfig.cs, SkillItem now ObservableObject |
| 🟢 Quality (10) | EnvironmentService merged helpers + process deadlock fix, InstallerService RunCommandAsync race fix, DashboardViewModel merged dual ReadJSON, AllCases static readonly, DashboardSummary RemoveRange+UtcNow, Windows version RuntimeInformation, fire-and-forget exception logging, Anthropic models extracted to constant |

---

## Key Features Since Initial Port

- **Auto Update** — checks GitHub Releases on startup, manual check button in sidebar, update banner with one-click download
- **Skill Marketplace** — fetches official skill list with real names/descriptions from SKILL.md, GitHub URL auto-detection in search bar, built-in offline fallback (37 skills), mirror retry for China network
- **Installer Panel** — one-click Claude Code CLI install/uninstall via npm/winget
- **Env Check Panel** — detects Node.js, npm, Git status and versions
- **Version Display** — sidebar footer shows `v1.1.0` + update status dot (green "已是最新" / grey "检查中…" / red "发现新版本")

---

## Project File Map

```
ClaudeCodePanel.Windows/
├── ClaudeCodePanel.Windows.sln
├── HANDOFF.md
├── README.md
├── .gitignore
├── ClaudeConsole-Portable.zip          ← latest portable build
├── release/ClaudeConsole.exe           ← latest self-contained single-file exe
└── src/ClaudeCodePanel.Windows/
    ├── ClaudeCodePanel.Windows.csproj  ← net9.0-windows, AssemblyName=ClaudeConsole, Version=1.1.0
    ├── App.xaml / App.xaml.cs          ← DI container, global exception handlers, lifecycle
    ├── Models/
    │   ├── APIProvider.cs              ← Anthropic/OpenAI/DeepSeek/Custom enum + extensions
    │   ├── DashboardSummary.cs         ← Dashboard data + recent events
    │   ├── IndicatorStatus.cs          ← Running/Stopped/Error enum
    │   ├── MCPServerConfig.cs          ← MCP server model + connection result
    │   ├── SkillItem.cs                ← Skill model (ObservableObject, source-gen properties)
    │   └── UpdateInfo.cs               ← GitHub release update info
    ├── Services/
    │   ├── ConfigFileService.cs        ← File I/O for ~/.claude/*.json, atomic writes
    │   ├── CredentialService.cs        ← Windows Credential Manager P/Invoke + TryRead
    │   ├── EnvironmentService.cs       ← Node/npm/Git detection with RunProcess helper
    │   ├── FileWatcherService.cs       ← Debounced file watcher (event Action)
    │   ├── InstallerService.cs         ← Claude Code CLI install/uninstall via npm
    │   ├── MCPService.cs              ← MCP connection testing (stdio + SSE)
    │   ├── SkillRepositoryService.cs   ← GitHub marketplace fetch + install + built-in offline list
    │   ├── SyncService.cs             ← Cross-process sync (EnumerateObject optimization)
    │   └── UpdateService.cs           ← GitHub Releases API version check
    ├── ViewModels/
    │   ├── MainViewModel.cs            ← Navigation hub + auto update + version display
    │   ├── DashboardViewModel.cs       ← Summary from config files + InstallerService
    │   ├── APIConfigViewModel.cs       ← Provider config, key mgmt, model detection
    │   ├── ConfigEditorViewModel.cs    ← File tabs, edit, save with conflict detection
    │   ├── MCPManagerViewModel.cs      ← Server CRUD, connection testing
    │   ├── SkillManagerViewModel.cs    ← Installed + marketplace skills, GitHub URL detection
    │   ├── InstallerViewModel.cs       ← Claude Code install/uninstall UI
    │   └── EnvCheckViewModel.cs        ← Environment dependency checks
    ├── Views/
    │   ├── MainWindow.xaml(.cs)        ← Custom title bar, Mica, sidebar, update banner
    │   ├── Sidebar/SidebarView.xaml(.cs) ← Nav items + version/update status footer
    │   ├── Dashboard/DashboardView.xaml(.cs)
    │   ├── API/APIConfigView.xaml(.cs), APIKeyInputView.xaml(.cs)
    │   ├── Config/ConfigEditorView.xaml(.cs)
    │   ├── MCP/MCPServerListView.xaml(.cs), MCPServerCard.xaml(.cs), MCPServerEditorView.xaml(.cs)
    │   ├── Skills/SkillsListView.xaml(.cs), SkillCard.xaml(.cs), SkillInstallDialog.xaml(.cs)
    │   ├── Installer/InstallerView.xaml(.cs)
    │   ├── EnvCheck/EnvCheckView.xaml(.cs)
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
    │   ├── MCPDisplayNameStore.cs     ← Display name persistence (debounced writes)
    │   └── Windows11Interop.cs        ← Mica P/Invoke + RuntimeInformation version check
    └── Resources/Themes/DarkTheme.xaml ← Global styles + DataTemplates
```

---

## Architecture Notes

- **Singleton services** use `private constructor` + `public static Instance { get; } = new()`, registered via `services.AddSingleton(Service.Instance)`
- **ViewModels** use CommunityToolkit.Mvvm `[ObservableProperty]` and `[RelayCommand]` source generators
- **MainWindow** receives `MainViewModel` via constructor injection (not Service Locator)
- **API config** reads Claude Code's `env.*` keys from settings.json via SyncService
- **Config writes** use read-merge-write with atomic `File.Move(temp, path, overwrite: true)`
- **GlassCard** is a ContentControl (not UserControl) to avoid WPF namescope conflicts
- **Global exception handlers** in App.xaml.cs catch unhandled dispatcher + AppDomain exceptions
- **Skill marketplace** has 3-tier fallback: GitHub API → mirror → built-in offline list (37 skills)

## Update Flow

```
Startup → CheckForUpdateAsync()
  → GET api.github.com/repos/Lyxxxx718/ClaudeConsole/releases/latest
  → Compare tag_name vs. hardcoded Version(1,0,0)
  → Newer? Show blue banner + "发现 vX.Y.Z" in sidebar
  → User clicks → browser opens release page
  → Sidebar button → manual re-check
```

## GitHub Release

- **Repo:** https://github.com/Lyxxxx718/ClaudeConsole
- **Release tag format:** `v1.0.0` (semver, optional `v` prefix)
- **Upload:** `release/ClaudeConsole.exe`
- **UpdateService.cs line 22:** bump `CurrentVersion` constant when releasing new version

## Publishing

```powershell
# Self-contained portable (no runtime needed)
dotnet publish src/ClaudeCodePanel.Windows -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o release/

# ZIP for distribution
powershell -Command "Compress-Archive -Path release/ClaudeConsole.exe -DestinationPath ClaudeConsole-Portable.zip -Force"
```

## Design Tokens

| Token | Value |
|-------|-------|
| Window background | `#1C1C1E` |
| Sidebar background | `#1A1A1D` |
| GlassCard fill | `#CC1A1A1A` (80% opacity) |
| Accent | `#007AFF` |
| Success | `#30D158` |
| Error | `#FF453A` |
| Corner radius | 14 (cards), 8 (inputs) |
| Config path | `%USERPROFILE%\.claude\` |
