# Claude Console Full Liquid Glass React UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace normal WPF page navigation with a complete React + `liquid-glass-react` workspace for all seven Claude Console workflows while retaining .NET business logic and native fallback.

**Architecture:** React owns routing, presentation, local drafts, motion, and task progress display. A typed WebView2 bridge exposes domain snapshots and commands backed by existing .NET ViewModels and Services; long-running commands emit task events. Dynamic refraction is restricted to shell and interaction surfaces, while text, forms, code, logs, and lists render on clear content layers.

**Tech Stack:** .NET 9, WPF, WebView2, C#, xUnit, React 19, TypeScript 5.7, Vite 6, Vitest, Testing Library, `liquid-glass-react`, Lucide React.

---

## File map

### React application

- Create `src/ClaudeCodePanel.WebUI/src/app/navigation.ts`: stable page metadata and route keys.
- Create `src/ClaudeCodePanel.WebUI/src/app/useAppController.ts`: bootstrap, route, task, host-event, and command state.
- Modify `src/ClaudeCodePanel.WebUI/src/types.ts`: domain snapshots, bridge command map, task events, and app state.
- Modify `src/ClaudeCodePanel.WebUI/src/bridge/webViewBridge.ts`: typed generic requests, timeout cleanup, host event subscriptions, and preview data.
- Modify `src/ClaudeCodePanel.WebUI/src/components/GlassSurface.tsx`: separate material and content layers and add reduced-effects behavior.
- Create `src/ClaudeCodePanel.WebUI/src/components/NavigationDock.tsx`: seven-destination icon dock.
- Create `src/ClaudeCodePanel.WebUI/src/components/WorkspaceCommandBar.tsx`: command entry, workspace state, and page action.
- Create `src/ClaudeCodePanel.WebUI/src/components/HealthStrip.tsx`: shared real-state health summary.
- Create `src/ClaudeCodePanel.WebUI/src/components/TaskShelf.tsx`: progress, quick actions, and environment summary.
- Create `src/ClaudeCodePanel.WebUI/src/components/ContextPanel.tsx`: shared right-side panel/drawer behavior.
- Modify `src/ClaudeCodePanel.WebUI/src/components/AppShell.tsx`: compose shell and route all pages internally.
- Modify `src/ClaudeCodePanel.WebUI/src/pages/DashboardPage.tsx`: approved full-height dashboard.
- Create `src/ClaudeCodePanel.WebUI/src/pages/ApiConfigPage.tsx`.
- Create `src/ClaudeCodePanel.WebUI/src/pages/ConfigEditorPage.tsx`.
- Create `src/ClaudeCodePanel.WebUI/src/pages/McpManagerPage.tsx`.
- Create `src/ClaudeCodePanel.WebUI/src/pages/SkillsPage.tsx`.
- Create `src/ClaudeCodePanel.WebUI/src/pages/InstallerPage.tsx`.
- Create `src/ClaudeCodePanel.WebUI/src/pages/EnvironmentPage.tsx`.
- Modify `src/ClaudeCodePanel.WebUI/src/styles.css`: approved layout, material, motion, responsive, focus, and reduced-motion styles.

### .NET host

- Modify `src/ClaudeCodePanel.Windows/WebUI/WebUiBridge.cs`: domain command routing and long-task event hooks.
- Modify `src/ClaudeCodePanel.Windows/WebUI/WebUiResponse.cs`: add explicit helpers for domain command failures and accepted long tasks.
- Create `src/ClaudeCodePanel.Windows/WebUI/WebUiSnapshots.cs`: serializable page and task records.
- Modify `src/ClaudeCodePanel.Windows/Views/MainWindow.xaml.cs`: push host events and keep native fallback isolated.
- Modify `src/ClaudeCodePanel.Windows/ClaudeCodePanel.Windows.csproj`: package the final Web UI output.

### Tests

- Modify `src/ClaudeCodePanel.WebUI/src/bridge/webViewBridge.test.ts`.
- Modify `src/ClaudeCodePanel.WebUI/src/components/GlassSurface.test.tsx`.
- Create `src/ClaudeCodePanel.WebUI/src/components/AppShell.test.tsx`.
- Modify `src/ClaudeCodePanel.WebUI/src/pages/DashboardPage.test.tsx`.
- Create one focused page test beside each new page.
- Modify `src/ClaudeCodePanel.Windows.Tests/WebUI/WebUiBridgeTests.cs`.
- Create `src/ClaudeCodePanel.Windows.Tests/WebUI/WebUiDomainSnapshotTests.cs`.

## Task 1: Expand the typed host protocol

**Files:**
- Modify: `src/ClaudeCodePanel.WebUI/src/types.ts`
- Modify: `src/ClaudeCodePanel.WebUI/src/bridge/webViewBridge.ts`
- Modify: `src/ClaudeCodePanel.WebUI/src/bridge/webViewBridge.test.ts`
- Create: `src/ClaudeCodePanel.Windows/WebUI/WebUiSnapshots.cs`
- Modify: `src/ClaudeCodePanel.Windows/WebUI/WebUiBridge.cs`
- Modify: `src/ClaudeCodePanel.Windows.Tests/WebUI/WebUiBridgeTests.cs`
- Create: `src/ClaudeCodePanel.Windows.Tests/WebUI/WebUiDomainSnapshotTests.cs`

- [ ] **Step 1: Write failing TypeScript protocol tests**

Add tests proving that `api.get`, `config.get`, `mcp.list`, `skills.list`, `installer.get`, and `environment.get` preserve typed payloads, and that `task.progress` reaches an event subscriber.

```ts
it('delivers task progress events', () => {
  const transport = createTransport()
  const bridge = createWebViewBridge(transport)
  const listener = vi.fn()
  bridge.subscribe('task.progress', listener)
  transport.emit({ type: 'task.progress', data: { id: 'skills-sync', progress: 0.68, label: 'Skills 同步中' } })
  expect(listener).toHaveBeenCalledWith(expect.objectContaining({ id: 'skills-sync', progress: 0.68 }))
})
```

- [ ] **Step 2: Run the focused web test and confirm RED**

Run: `npm test -- src/bridge/webViewBridge.test.ts`

Expected: FAIL because domain commands and `subscribe` do not exist.

- [ ] **Step 3: Define domain snapshots and bridge command types**

Add explicit types for API providers/models, config files/conflicts, MCP servers/tests, Skills/search/install state, installer state/logs, environment dependencies, task state, and host events. Export one `BridgeRequestMap` used by the bridge client.

```ts
export interface TaskSnapshot {
  id: string
  label: string
  progress: number | null
  status: 'running' | 'success' | 'error'
  detail?: string
}

export interface HostEventMap {
  'task.started': TaskSnapshot
  'task.progress': TaskSnapshot
  'task.completed': TaskSnapshot
  'task.failed': TaskSnapshot
  'theme.changed': ThemeSnapshot
}
```

- [ ] **Step 4: Implement event-aware bridge behavior**

Requests must delete pending callbacks on success, timeout, and disposal. Host messages without an `id` and with a known event `type` must be delivered to subscribers.

```ts
subscribe<T extends keyof HostEventMap>(type: T, listener: (value: HostEventMap[T]) => void) {
  const listeners = eventListeners.get(type) ?? new Set()
  listeners.add(listener as (value: unknown) => void)
  eventListeners.set(type, listeners)
  return () => listeners.delete(listener as (value: unknown) => void)
}
```

- [ ] **Step 5: Write failing xUnit routing tests**

Add one theory for valid read commands and facts for invalid payload, unknown command, and command failure mapping.

```csharp
[Theory]
[InlineData("api.get")]
[InlineData("config.get")]
[InlineData("mcp.list")]
[InlineData("skills.list")]
[InlineData("installer.get")]
[InlineData("environment.get")]
public async Task HandleAsync_KnownReadCommand_ReturnsSuccess(string type)
{
    var response = await _bridge.HandleAsync($$"""{"id":"1","type":"{{type}}"}""");
    Assert.True(response.Ok);
}
```

- [ ] **Step 6: Run focused xUnit tests and confirm RED**

Run: `dotnet test src/ClaudeCodePanel.Windows.Tests --filter "FullyQualifiedName~WebUI"`

Expected: FAIL because the domain routes and snapshots do not exist.

- [ ] **Step 7: Add serializable snapshot records and domain handlers**

Use injected delegates around existing ViewModels/Services. Do not place credentials in snapshots. Convert command exceptions to `command_failed` and validation failures to `invalid_payload`.

- [ ] **Step 8: Run protocol tests and confirm GREEN**

Run: `npm test -- src/bridge/webViewBridge.test.ts`

Run: `dotnet test src/ClaudeCodePanel.Windows.Tests --filter "FullyQualifiedName~WebUI"`

Expected: all focused tests PASS.

- [ ] **Step 9: Commit the protocol slice**

```powershell
git add src/ClaudeCodePanel.WebUI/src/types.ts src/ClaudeCodePanel.WebUI/src/bridge src/ClaudeCodePanel.Windows/WebUI src/ClaudeCodePanel.Windows.Tests/WebUI
git commit -m "feat: expand web ui domain protocol"
```

## Task 2: Build the approved liquid workspace shell

**Files:**
- Create: `src/ClaudeCodePanel.WebUI/src/app/navigation.ts`
- Create: `src/ClaudeCodePanel.WebUI/src/app/useAppController.ts`
- Create: `src/ClaudeCodePanel.WebUI/src/components/NavigationDock.tsx`
- Create: `src/ClaudeCodePanel.WebUI/src/components/WorkspaceCommandBar.tsx`
- Create: `src/ClaudeCodePanel.WebUI/src/components/ContextPanel.tsx`
- Modify: `src/ClaudeCodePanel.WebUI/src/components/GlassSurface.tsx`
- Modify: `src/ClaudeCodePanel.WebUI/src/components/GlassSurface.test.tsx`
- Modify: `src/ClaudeCodePanel.WebUI/src/components/AppShell.tsx`
- Create: `src/ClaudeCodePanel.WebUI/src/components/AppShell.test.tsx`
- Modify: `src/ClaudeCodePanel.WebUI/src/styles.css`

- [ ] **Step 1: Write failing shell and glass tests**

Test that all seven routes remain in React, selected navigation exposes `aria-current="page"`, reduced effects render a CSS fallback, and material content is outside the refractive layer.

```tsx
expect(screen.getByRole('button', { name: 'API 配置' })).toHaveAttribute('aria-current', 'page')
expect(container.querySelector('.glass-surface__material')).toBeInTheDocument()
expect(container.querySelector('.glass-surface__content')).toBeInTheDocument()
```

- [ ] **Step 2: Run shell tests and confirm RED**

Run: `npm test -- src/components/AppShell.test.tsx src/components/GlassSurface.test.tsx`

Expected: FAIL because the new shell components and layer split do not exist.

- [ ] **Step 3: Implement navigation metadata and controller**

Keep route keys stable and change routes locally without calling `shell.native`. The controller owns bootstrap, active page, current task, page error, and event subscriptions.

- [ ] **Step 4: Implement shell components**

Build the narrow icon dock, workspace/command bar, and responsive context panel. Use Lucide icons and semantic buttons; do not build icons from CSS or inline SVG.

- [ ] **Step 5: Separate glass material from content**

The material layer uses `LiquidGlass`; the content layer remains a sibling above it. Interactive transforms apply to the outer frame and never distort text.

```tsx
return (
  <div className={`glass-frame ${className}`} data-preset={preset}>
    <div className="glass-surface__material" aria-hidden="true">
      <LiquidGlass {...config}><span /></LiquidGlass>
    </div>
    <div className="glass-surface__content">{children}</div>
  </div>
)
```

- [ ] **Step 6: Implement approved shell and motion tokens**

Add CSS custom properties for 90/140/180/220/240/260/420ms durations, shared easing, focus rings, active nav bulge, page morph, hover edge-light, press compression, and `prefers-reduced-motion` overrides.

- [ ] **Step 7: Run shell tests and confirm GREEN**

Run: `npm test -- src/components/AppShell.test.tsx src/components/GlassSurface.test.tsx`

Run: `npm run typecheck`

Expected: PASS with no TypeScript errors.

- [ ] **Step 8: Commit the shell slice**

```powershell
git add src/ClaudeCodePanel.WebUI/src/app src/ClaudeCodePanel.WebUI/src/components src/ClaudeCodePanel.WebUI/src/styles.css
git commit -m "feat: build immersive liquid workspace shell"
```

## Task 3: Implement dashboard health, pulse, activity, and task shelf

**Files:**
- Create: `src/ClaudeCodePanel.WebUI/src/components/HealthStrip.tsx`
- Create: `src/ClaudeCodePanel.WebUI/src/components/TaskShelf.tsx`
- Modify: `src/ClaudeCodePanel.WebUI/src/pages/DashboardPage.tsx`
- Modify: `src/ClaudeCodePanel.WebUI/src/pages/DashboardPage.test.tsx`
- Modify: `src/ClaudeCodePanel.WebUI/src/styles.css`

- [ ] **Step 1: Write failing dashboard tests**

Test real health values, a stable no-task shelf, running task progress, two quick actions, and no legacy metric card grid.

```tsx
expect(screen.getByRole('region', { name: '系统健康' })).toHaveTextContent('3 / 4')
expect(screen.getByRole('region', { name: '任务与快捷操作' })).toHaveTextContent('最近任务')
expect(container.querySelector('.metrics-grid')).not.toBeInTheDocument()
```

- [ ] **Step 2: Run dashboard tests and confirm RED**

Run: `npm test -- src/pages/DashboardPage.test.tsx`

Expected: FAIL because the approved regions do not exist.

- [ ] **Step 3: Implement derived session health history**

Append one bounded sample after each dashboard refresh. Compute a 0-100 health value only from real booleans/counts already present in `DashboardSnapshot`; retain at most 24 samples in memory.

- [ ] **Step 4: Implement the full-height dashboard**

Use the selected composition: health strip, large pulse graph, right activity timeline, and bottom task shelf. Render the chart with semantic HTML/CSS or a project dependency already present; do not add decorative canvas data or fake metrics.

- [ ] **Step 5: Add dashboard motion states**

Reveal the pulse path once per new sample, stagger only newly mounted activity items, spin refresh while pending, and expand the task shelf only when a task starts.

- [ ] **Step 6: Run dashboard tests and confirm GREEN**

Run: `npm test -- src/pages/DashboardPage.test.tsx`

Run: `npm run typecheck`

Expected: PASS.

- [ ] **Step 7: Commit the dashboard slice**

```powershell
git add src/ClaudeCodePanel.WebUI/src/components/HealthStrip.tsx src/ClaudeCodePanel.WebUI/src/components/TaskShelf.tsx src/ClaudeCodePanel.WebUI/src/pages/DashboardPage.tsx src/ClaudeCodePanel.WebUI/src/pages/DashboardPage.test.tsx src/ClaudeCodePanel.WebUI/src/styles.css
git commit -m "feat: implement liquid glass dashboard"
```

## Task 4: Migrate API configuration and config editor

**Files:**
- Create: `src/ClaudeCodePanel.WebUI/src/pages/ApiConfigPage.tsx`
- Create: `src/ClaudeCodePanel.WebUI/src/pages/ApiConfigPage.test.tsx`
- Create: `src/ClaudeCodePanel.WebUI/src/pages/ConfigEditorPage.tsx`
- Create: `src/ClaudeCodePanel.WebUI/src/pages/ConfigEditorPage.test.tsx`
- Modify: `src/ClaudeCodePanel.WebUI/src/components/AppShell.tsx`
- Modify: `src/ClaudeCodePanel.WebUI/src/styles.css`

- [ ] **Step 1: Write failing API page tests**

Test provider selection, password input, dirty state, save, connection test, and model detection. Assert that the password value is submitted to the host but never written to storage APIs.

- [ ] **Step 2: Write failing config editor tests**

Test file selection, dirty draft preservation, save, external-change conflict display, and confirm-before-switch behavior.

- [ ] **Step 3: Run both page tests and confirm RED**

Run: `npm test -- src/pages/ApiConfigPage.test.tsx src/pages/ConfigEditorPage.test.tsx`

Expected: FAIL because both pages are absent.

- [ ] **Step 4: Implement API configuration**

Use a clear main form and right context panel. Drive all mutations through bridge commands, disable duplicate submissions, keep failed drafts, and surface progress in `TaskShelf`.

- [ ] **Step 5: Implement config editor**

Use file navigation, clear code content surface, metadata/conflict panel, save action, and unsaved-change dialog. Keep glass out of the editable text plane.

- [ ] **Step 6: Route both pages internally**

Selecting either destination must update `PageRouter`; it must not request native fallback.

- [ ] **Step 7: Run tests and confirm GREEN**

Run: `npm test -- src/pages/ApiConfigPage.test.tsx src/pages/ConfigEditorPage.test.tsx`

Run: `npm run typecheck`

Expected: PASS.

- [ ] **Step 8: Commit the configuration slice**

```powershell
git add src/ClaudeCodePanel.WebUI/src/pages/ApiConfigPage* src/ClaudeCodePanel.WebUI/src/pages/ConfigEditorPage* src/ClaudeCodePanel.WebUI/src/components/AppShell.tsx src/ClaudeCodePanel.WebUI/src/styles.css
git commit -m "feat: migrate configuration workflows to react"
```

## Task 5: Migrate MCP services and Skills

**Files:**
- Create: `src/ClaudeCodePanel.WebUI/src/pages/McpManagerPage.tsx`
- Create: `src/ClaudeCodePanel.WebUI/src/pages/McpManagerPage.test.tsx`
- Create: `src/ClaudeCodePanel.WebUI/src/pages/SkillsPage.tsx`
- Create: `src/ClaudeCodePanel.WebUI/src/pages/SkillsPage.test.tsx`
- Modify: `src/ClaudeCodePanel.WebUI/src/components/AppShell.tsx`
- Modify: `src/ClaudeCodePanel.WebUI/src/styles.css`

- [ ] **Step 1: Write failing MCP tests**

Test search, select, add/edit drawer, rename, delete confirmation, connection test progress, and retained error output.

- [ ] **Step 2: Write failing Skills tests**

Test installed/marketplace switching, search, GitHub URL state, refresh, install/uninstall progress, and empty/offline states.

- [ ] **Step 3: Run tests and confirm RED**

Run: `npm test -- src/pages/McpManagerPage.test.tsx src/pages/SkillsPage.test.tsx`

Expected: FAIL because both pages are absent.

- [ ] **Step 4: Implement MCP manager**

Use one grouped server list with row separators and a right editor drawer. Long tests publish task events and remain navigable.

- [ ] **Step 5: Implement Skills**

Use high-density rows plus detail panel. Keep repository search/fallback in .NET and expose progress through the task shelf.

- [ ] **Step 6: Run tests and confirm GREEN**

Run: `npm test -- src/pages/McpManagerPage.test.tsx src/pages/SkillsPage.test.tsx`

Run: `npm run typecheck`

Expected: PASS.

- [ ] **Step 7: Commit the extensions slice**

```powershell
git add src/ClaudeCodePanel.WebUI/src/pages/McpManagerPage* src/ClaudeCodePanel.WebUI/src/pages/SkillsPage* src/ClaudeCodePanel.WebUI/src/components/AppShell.tsx src/ClaudeCodePanel.WebUI/src/styles.css
git commit -m "feat: migrate mcp and skills workflows"
```

## Task 6: Migrate installer and environment checks

**Files:**
- Create: `src/ClaudeCodePanel.WebUI/src/pages/InstallerPage.tsx`
- Create: `src/ClaudeCodePanel.WebUI/src/pages/InstallerPage.test.tsx`
- Create: `src/ClaudeCodePanel.WebUI/src/pages/EnvironmentPage.tsx`
- Create: `src/ClaudeCodePanel.WebUI/src/pages/EnvironmentPage.test.tsx`
- Modify: `src/ClaudeCodePanel.WebUI/src/components/AppShell.tsx`
- Modify: `src/ClaudeCodePanel.WebUI/src/styles.css`

- [ ] **Step 1: Write failing installer tests**

Test installed/uninstalled state, install, uninstall confirmation, live log append, retry, and task progress.

- [ ] **Step 2: Write failing environment tests**

Test Node/npm/Git rows, per-item progress, version/path, check-all, failure repair action, and environment summary reuse.

- [ ] **Step 3: Run tests and confirm RED**

Run: `npm test -- src/pages/InstallerPage.test.tsx src/pages/EnvironmentPage.test.tsx`

Expected: FAIL because both pages are absent.

- [ ] **Step 4: Implement installer**

Render the primary status/action surface, clear log content plane, progress, and recoverable failure state.

- [ ] **Step 5: Implement environment check**

Render one grouped dependency list and remediation actions. Reuse the same environment health mapper used by the task shelf.

- [ ] **Step 6: Run tests and confirm GREEN**

Run: `npm test -- src/pages/InstallerPage.test.tsx src/pages/EnvironmentPage.test.tsx`

Run: `npm run typecheck`

Expected: PASS.

- [ ] **Step 7: Commit the system slice**

```powershell
git add src/ClaudeCodePanel.WebUI/src/pages/InstallerPage* src/ClaudeCodePanel.WebUI/src/pages/EnvironmentPage* src/ClaudeCodePanel.WebUI/src/components/AppShell.tsx src/ClaudeCodePanel.WebUI/src/styles.css
git commit -m "feat: migrate system workflows to react"
```

## Task 7: Integrate host events, package assets, and perform design QA

**Files:**
- Modify: `src/ClaudeCodePanel.Windows/Views/MainWindow.xaml.cs`
- Modify: `src/ClaudeCodePanel.Windows/ClaudeCodePanel.Windows.csproj`
- Modify: `src/ClaudeCodePanel.WebUI/src/components/AppShell.test.tsx`
- Modify: `src/ClaudeCodePanel.Windows.Tests/WebUI/WebUiBridgeTests.cs`
- Update: `docs/superpowers/plans/2026-07-16-full-liquid-glass-react-ui.md`

- [ ] **Step 1: Write failing integration tests**

Assert all seven navigation selections return `useNativeShell: false`, host task events serialize correctly, and malformed/unknown commands still fail safely.

- [ ] **Step 2: Run integration tests and confirm RED**

Run: `dotnet test src/ClaudeCodePanel.Windows.Tests --filter "FullyQualifiedName~WebUI"`

Run: `npm test -- src/components/AppShell.test.tsx`

Expected: FAIL until final host behavior is connected.

- [ ] **Step 3: Connect host event publishing and web-only navigation**

Push typed events through WebView2, keep native fallback available only on initialization/resource failure, and prevent normal route changes from revealing WPF pages.

- [ ] **Step 4: Build and package the Web UI**

Run: `npm run build`

Expected: Vite production build succeeds and the .NET project copies it into packaged `WebUI` assets.

- [ ] **Step 5: Run the complete automated suite**

Run: `npm test`

Run: `npm run typecheck`

Run: `npm run build`

Run: `dotnet test ClaudeCodePanel.Windows.sln -c Release`

Run: `dotnet build ClaudeCodePanel.Windows.sln -c Release`

Expected: all commands exit 0 with no test failures.

- [ ] **Step 6: Capture implementation screenshots**

Run the local preview and capture 1440×1024, 1024×768, and 820×640 states for Dashboard plus at least one form page and one list page.

- [ ] **Step 7: Compare source and implementation together**

Compare the approved target at `docs/superpowers/specs/assets/2026-07-16-full-liquid-glass-react-ui-target.png` with the 1440×1024 Dashboard screenshot in one visual input. Fix visible hierarchy, spacing, clipping, radius, focus, and text-clarity mismatches, then repeat the comparison once.

- [ ] **Step 8: Verify interaction and fallback states manually**

Check hover refraction, press compression, nav morph, page transition, chart reveal, activity entry, task shelf expansion, reduced motion, CSS glass fallback, light theme, and forced missing-WebUI native fallback.

- [ ] **Step 9: Mark completed plan checkboxes and commit integration**

```powershell
git add src/ClaudeCodePanel.Windows src/ClaudeCodePanel.Windows.Tests src/ClaudeCodePanel.WebUI docs/superpowers/plans/2026-07-16-full-liquid-glass-react-ui.md
git commit -m "feat: complete full liquid glass react ui"
```
