# ClaudeConsole Handoff

## Project snapshot

ClaudeConsole is a Windows desktop manager for Claude Code configuration. It targets `net9.0-windows10.0.19041.0` and currently reports version `1.2.2` from the main project file.

The application has two presentation layers:

- A React 19 + TypeScript workspace hosted in WebView2. It owns the liquid-glass shell, responsive navigation, dashboard, and seven workspace presentations.
- Native WPF panels backed by the existing view models and services. They remain the safe operational fallback while bridge coverage expands.

## Current verification baseline

Freshly verified on 2026-07-16:

- Web UI: 14 Vitest tests passing across 4 test files.
- Web UI type check: `tsc --noEmit` passing.
- Web UI production build: Vite build passing.
- .NET: 186 xUnit tests passing in Release configuration.

Use the commands in `README.md` to reproduce this baseline. Never commit generated `node_modules/`, `dist/`, `bin/`, `obj/`, publish directories, or portable ZIP files.

## Important architecture boundary

`WebUiBridge` currently handles:

- `app.ready`
- `dashboard.get`
- `theme.get`
- `navigation.select`
- `shell.native`

The React workspace uses live dashboard and theme data. Other React pages currently manage presentation/local interaction state; operational mutations still belong to the native WPF view models and services. Do not describe all React workflows as fully bridge-backed until explicit domain commands and tests exist.

If WebView2 initialization, navigation, assets, or the renderer process fail, `MainWindow` reveals the native workspace automatically. The user can also request the native shell explicitly.

## Project map

```text
ClaudeCodePanel.Windows.sln
├── src/ClaudeCodePanel.WebUI/
│   ├── src/app/                 navigation metadata
│   ├── src/bridge/              typed WebView client and tests
│   ├── src/components/          liquid shell, navigation, health, task surfaces
│   ├── src/pages/               dashboard and six workflow presentations
│   └── package.json             test, typecheck, dev, and build scripts
├── src/ClaudeCodePanel.Windows/
│   ├── Design/                  deterministic UI policy types
│   ├── Models/                  domain and presentation records
│   ├── Services/                configuration, credentials, install, MCP, skills, update
│   ├── ViewModels/              native workflow state and commands
│   ├── Views/                   WebView2 host and native WPF fallback
│   ├── WebUI/                   host message protocol and asset locator
│   └── Resources/Themes/        shared, dark, and light design resources
├── src/ClaudeCodePanel.Windows.Tests/
│   ├── Services/                service behavior and process/file-system regression tests
│   ├── ViewModels/              command and state tests
│   ├── UI/                      layout and appearance policies
│   └── WebUI/                   bridge and asset location tests
└── docs/superpowers/            design specifications, plans, and visual evidence
```

## Build flow

The WPF project invokes `npm ci` when Web UI dependencies are absent, runs the Vite production build, and copies `dist/**` into the application output under `WebUI/`. `WebUiAssetLocator` resolves those packaged assets at runtime.

Main verification:

```powershell
npm test --prefix src/ClaudeCodePanel.WebUI
npm run typecheck --prefix src/ClaudeCodePanel.WebUI
npm run build --prefix src/ClaudeCodePanel.WebUI
dotnet test ClaudeCodePanel.Windows.sln --configuration Release
dotnet build ClaudeCodePanel.Windows.sln --configuration Release
```

Portable x64 package:

```powershell
dotnet publish src/ClaudeCodePanel.Windows/ClaudeCodePanel.Windows.csproj `
  --configuration Release `
  --framework net9.0-windows10.0.19041.0 `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=none `
  --output publish/win-x64
```

## Data and security rules

- API keys remain in Windows Credential Manager and must never be serialized into WebView snapshots.
- Claude configuration writes use merge/backup/atomic-write behavior; preserve conflict detection.
- Long-running process execution must remain asynchronous and time-bounded.
- Web pages must not access `window.chrome.webview` directly; use the bridge client.
- Keep text, forms, code, logs, and long lists outside the refractive material layer.

## Release notes

- Repository: `RRRadiant/ClaudeConsole`
- Default branch: `main`
- Release tags use semantic versions such as `v1.2.2`.
- The application update check compares the newest GitHub release tag with the assembly version.
- Bump `<Version>` in `src/ClaudeCodePanel.Windows/ClaudeCodePanel.Windows.csproj` only when preparing an actual release.

## Known follow-up

The next major architecture slice is to add typed bridge snapshots and mutation commands for API, config, MCP, skills, installer, and environment workflows, then replace local React-only state with service-backed state. Preserve the native fallback until each domain has success, failure, and recovery coverage.
