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

**Current:** 0 errors, 0 warnings, 92 tests passing — clean.

### v1.1.0 – Code Audit Fixes (13 items)

| Category | Changes |
|----------|---------|
| 🔴 Fixes (2) | EnvironmentService.RunProcess .Result deadlock → async, InstallerService sync WaitForExit → async |
| 🟠 Architecture (6) | 7 ViewModels support constructor injection (optional param + Instance fallback), MainViewModel caches resolved ViewModels, MCPService Process.Exited leak fix, silent catch blocks now log Debug.WriteLine, async void handlers wrapped in try/catch, ConfigFileService.TryReadJSON added |
| 🟡 Quality (5) | SharedHelpers extracted (CapitalizeWords, EnumerateJsonObject), xUnit test project with 92 tests, unused usings removed, ConfigFileType equality tests, SkillRepositoryService frontmatter parsing tests |

### v1.2.0 – Quality & Architecture Upgrade

| Category | Changes |
|----------|---------|
| 🔴 Fixes (17) | All 14+ empty catch blocks now log via Debug.WriteLine; UpdateService distinguishes "no update" from "check failed"; CredentialService.Read replaced with TryRead (no exception-as-control-flow); MCPServerListView OnEditorSave empty catch fixed |
| 🟠 Architecture (4) | Version unified — reads from Assembly.GetExecutingAssembly().GetName().Version (single source of truth: .csproj); MCPService TestStdioConnectionAsync stderr race eliminated (direct async pattern, removed Exited event+tcs+cts); git clone WaitForExit → WaitForExitAsync with 3-minute timeout; PlatformTarget x64 → AnyCPU (ARM64 support) |
| 🟡 Quality (3) | Duplicate CapitalizeWords removed (SkillRepositoryService + SkillManagerViewModel now use SharedHelpers); MCPManagerViewModel.SaveServerAsync fake async → void SaveServer; unused System.Globalization usings removed |

### v1.1.1 – npm Detection Fix

- `.cmd`/`.bat` files now run via `cmd.exe /c` explicitly (matching InstallerService.RunCommandAsync pattern)
- `CancellationTokenSource` → `Task.WhenAny + Task.Delay` (eliminates cancellation races)
- `FindInPathAsync` adds `.cmd`/`.exe` extension fallback + `where npm.cmd` retry

---

## Key Features Since Initial Port

- **Auto Update** — checks GitHub Releases on startup, manual check button in sidebar, update banner with one-click download
- **Skill Marketplace** — fetches official skill list with real names/descriptions from SKILL.md, GitHub URL auto-detection in search bar, built-in offline fallback (37 skills), mirror retry for China network
- **Installer Panel** — one-click Claude Code CLI install/uninstall via npm/winget
- **Env Check Panel** — detects Node.js, npm, Git status and versions
- **Dark/Light Theme** — complete dual design system, persisted preference, sidebar toggle
- **I18n Support** — Chinese/English resource files (30+ strings), runtime language switching
- **Design System** — typography scale, spacing scale (4px grid), corner radius tokens
- **GlassCard Micro-interactions** — hover scale animation, border color transition
- **Premium Sidebar** — rainbow-gradient brand logo, PREFERENCES section, accent update button

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
    ├── ClaudeCodePanel.Windows.csproj  ← net9.0-windows, AssemblyName=ClaudeConsole, Version=1.2.0
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

- **Singleton services** use `private constructor` + `public static Instance { get; } = new()`
- **ViewModels** accept optional constructor injection (`service ?? Service.Instance` fallback), enabling unit testing
- **InternalsVisibleTo** set on main project so test project can access `internal` members
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
  → GET api.github.com/repos/RRRadiant/ClaudeConsole/releases/latest
  → Compare tag_name vs. assembly version (no more hardcoded constant)
  → Newer? Show blue banner + "发现 vX.Y.Z" in sidebar
  → User clicks → browser opens release page
  → Sidebar button → manual re-check
```

## GitHub Release

- **Repo:** https://github.com/RRRadiant/ClaudeConsole
- **Release tag format:** `v1.2.0` (semver, optional `v` prefix)
- **Upload:** `release/ClaudeConsole.exe` + `ClaudeConsole-Portable.zip`
- **Version is read from assembly** — just bump `<Version>` in `.csproj` when releasing

## Publishing

```powershell
# Self-contained portable (no runtime needed) — supports win-x64 and win-arm64
dotnet publish src/ClaudeCodePanel.Windows -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o release/

# ZIP for distribution
powershell -Command "Compress-Archive -Path release/ClaudeConsole.exe -DestinationPath ClaudeConsole-Portable.zip -Force"
```

## Design Tokens

### Color System

| Token | Dark | Light |
|-------|------|-------|
| Window bg | `#080d1f` | `#f8f9fa` |
| Sidebar bg | `#0c0e18` | `#ffffff` |
| Accent | `#6faadd` | `#2563eb` |
| Text primary | `#F2FFFFFF` | `#111827` |
| Text secondary | `#99FFFFFF` | `#6b7280` |
| Success | `#34d399` | `#059669` |
| Error | `#f87171` | `#dc2626` |

### Scale Tokens

| Scale | Values |
|-------|--------|
| Radius | Sm(4) Md(6) Lg(8) Xl(12) 2xl(16) 3xl(24) 4xl(32) |
| Font | Xs(11) Sm(12) Base(14) Lg(16) Xl(18) 2xl(24) 3xl(30) |
| Spacing | 1(4px) 2(8) 3(12) 4(16) 5(20) 6(24) 8(32) 10(40) |

| Config path | `%USERPROFILE%\\.claude\\` |
