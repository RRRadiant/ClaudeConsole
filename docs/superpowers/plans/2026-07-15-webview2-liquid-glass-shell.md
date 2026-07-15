# Claude Console WebView2 Liquid Glass Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a production-buildable React application shell using the real `liquid-glass-react` package, hosted by WebView2 with a tested C# bridge and automatic WPF fallback.

**Architecture:** Keep all native services, view models, and WPF pages. Add a pure bridge layer between typed JSON messages and existing view models, package a Vite React app as static assets, and reveal the WebView2 shell only after React reports readiness.

**Tech Stack:** .NET 9, WPF, Microsoft.Web.WebView2, React 19, TypeScript, Vite, Vitest, Testing Library, liquid-glass-react.

---

### Task 1: Define and test the host protocol

**Files:**
- Create: `src/ClaudeCodePanel.Windows.Tests/WebUI/WebUiBridgeTests.cs`
- Create: `src/ClaudeCodePanel.Windows/WebUI/WebUiMessage.cs`
- Create: `src/ClaudeCodePanel.Windows/WebUI/WebUiResponse.cs`
- Create: `src/ClaudeCodePanel.Windows/WebUI/DashboardSnapshot.cs`
- Create: `src/ClaudeCodePanel.Windows/WebUI/WebUiBridge.cs`

- [ ] Write tests for malformed JSON, unknown commands, dashboard mapping, theme state, and known navigation.
- [ ] Run the focused xUnit tests and confirm they fail because the protocol types do not exist.
- [ ] Implement the smallest protocol and bridge handlers needed by the tests.
- [ ] Run the focused tests and confirm they pass.

### Task 2: Build the React shell test-first

**Files:**
- Create: `src/ClaudeCodePanel.WebUI/package.json`
- Create: `src/ClaudeCodePanel.WebUI/vite.config.ts`
- Create: `src/ClaudeCodePanel.WebUI/src/bridge/webViewBridge.ts`
- Create: `src/ClaudeCodePanel.WebUI/src/components/GlassSurface.tsx`
- Create: `src/ClaudeCodePanel.WebUI/src/components/AppShell.tsx`
- Create: `src/ClaudeCodePanel.WebUI/src/pages/DashboardPage.tsx`
- Create matching Vitest files under `src/ClaudeCodePanel.WebUI/src`.

- [ ] Scaffold Vite configuration and install React 19, `liquid-glass-react`, Vitest, jsdom, and Testing Library.
- [ ] Write failing tests for bridge fallback, preset values, and dashboard rendering.
- [ ] Run Vitest and confirm expected failures.
- [ ] Implement the bridge client, centralized glass presets, responsive shell, and dashboard.
- [ ] Run tests and production build.

### Task 3: Host Web UI with safe fallback

**Files:**
- Modify: `src/ClaudeCodePanel.Windows/ClaudeCodePanel.Windows.csproj`
- Modify: `src/ClaudeCodePanel.Windows/App.xaml.cs`
- Modify: `src/ClaudeCodePanel.Windows/Views/MainWindow.xaml`
- Modify: `src/ClaudeCodePanel.Windows/Views/MainWindow.xaml.cs`
- Create: `src/ClaudeCodePanel.Windows/WebUI/WebUiAssetLocator.cs`

- [ ] Add WebView2 and bridge services.
- [ ] Add the WebView2 composition host above the unchanged native shell.
- [ ] Initialize virtual-host assets, process messages, synchronize theme, and switch only after `app.ready`.
- [ ] Route non-dashboard navigation to existing WPF pages and expose a native-shell action.
- [ ] Copy the Vite build into `WebUI` during build and publish.

### Task 4: Verify the vertical slice

**Files:**
- Modify tests only for confirmed regressions.

- [ ] Run React tests and production build.
- [ ] Run focused bridge tests and the full .NET suite.
- [ ] Run the .NET Release build and inspect warnings/errors.
- [ ] Launch the application and verify the live WebView2 shell, theme behavior, native navigation handoff, window resizing, and fallback behavior.
- [ ] Review the final diff to confirm existing WPF business workflows remain intact.
