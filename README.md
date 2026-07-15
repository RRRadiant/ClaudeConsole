# ClaudeConsole

ClaudeConsole is a Windows desktop app for managing [Claude Code](https://claude.ai/code) configuration: API providers, models, config files, MCP servers, skills, installation, and local environment health.

It is a Windows port of the macOS [ClaudeCodePanel](https://github.com/RRRadiant/ClaudeCodePanel), built with a React/WebView2 workspace and a native WPF fallback.

## Features

| Workspace | What it manages |
|---|---|
| Dashboard | Claude Code, API, model, MCP, skill, and recent-activity status |
| API Config | Anthropic, OpenAI, DeepSeek, and custom providers; credentials use Windows Credential Manager |
| Config Editor | Claude Code configuration files with conflict detection and backup writes |
| MCP Servers | Server definitions, local display names, and connection tests |
| Skills | Installed skills, GitHub marketplace discovery, install, uninstall, and enable state |
| Installer | Claude Code CLI installation and removal through npm or winget |
| Environment | Node.js, npm, and Git detection, versions, and paths |

The React workspace provides the liquid-glass shell, dashboard, responsive navigation, and presentation for all seven workspaces. The .NET host currently supplies dashboard and theme data through a typed WebView2 bridge. Native WPF panels remain the operational fallback for business workflows that have not yet been connected to the bridge.

## Requirements

- Windows 10 version 2004 or later; Windows 11 is recommended
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js](https://nodejs.org/) with npm
- Visual Studio 2022 is optional

Packaged self-contained builds do not require a separate .NET runtime. They still require the Microsoft Edge WebView2 Runtime, which is normally present on supported Windows installations; the native WPF fallback remains available if WebView2 cannot start.

## Build from source

```powershell
git clone https://github.com/RRRadiant/ClaudeConsole.git
cd ClaudeConsole

npm ci --prefix src/ClaudeCodePanel.WebUI
npm test --prefix src/ClaudeCodePanel.WebUI
npm run build --prefix src/ClaudeCodePanel.WebUI

dotnet restore ClaudeCodePanel.Windows.sln
dotnet test ClaudeCodePanel.Windows.sln --configuration Release
dotnet run --configuration Release --project src/ClaudeCodePanel.Windows
```

The .NET project also builds the Web UI automatically and copies its generated assets into the application output.

## Create a portable package

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

Compress-Archive `
  -Path publish/win-x64/* `
  -DestinationPath ClaudeConsole-Portable.zip `
  -Force
```

`publish/`, `release/`, and the portable ZIP are ignored by Git so local packages do not enter source commits.

## Architecture

| Layer | Technology | Responsibility |
|---|---|---|
| Desktop host | .NET 9, WPF, WebView2 | Window lifecycle, native fallback, OS integration, asset hosting |
| Web workspace | React 19, TypeScript, Vite | Liquid-glass presentation, routing, responsive interaction |
| Host bridge | Typed JSON messages | Dashboard/theme bootstrap, refresh, navigation, fallback |
| Application logic | CommunityToolkit.Mvvm services and view models | Claude configuration and operational workflows |
| Storage | System.Text.Json, Windows Credential Manager | Local configuration and protected credentials |

```text
src/
├── ClaudeCodePanel.WebUI/          React workspace, bridge client, tests, and Vite build
├── ClaudeCodePanel.Windows/        WPF host, services, view models, native views, and host bridge
└── ClaudeCodePanel.Windows.Tests/  xUnit service, view-model, UI-policy, and WebView bridge tests

docs/superpowers/
├── specs/                          Design specifications and visual evidence
└── plans/                          Implementation plans
```

## Verification

```powershell
npm test --prefix src/ClaudeCodePanel.WebUI
npm run typecheck --prefix src/ClaudeCodePanel.WebUI
npm run build --prefix src/ClaudeCodePanel.WebUI
dotnet test ClaudeCodePanel.Windows.sln --configuration Release
```

## Download

Published versions are available from [GitHub Releases](https://github.com/RRRadiant/ClaudeConsole/releases).

## License

See [LICENSE](LICENSE). The project follows the licensing terms of the original ClaudeCodePanel project.
