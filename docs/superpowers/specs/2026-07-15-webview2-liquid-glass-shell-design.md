# Claude Console WebView2 Liquid Glass Shell

## Objective

Use the real `rdev/liquid-glass-react` implementation inside Claude Console without replacing its proven .NET services, view models, persistence, or native WPF workflows. The first release is a vertical slice that makes the new visual direction directly testable while preserving a safe native fallback.

## Scope

The React surface owns the application shell, navigation, page toolbar, dashboard summary, recent activity, theme-aware background, and explicit loading, empty, error, focus, hover, pressed, and reduced-motion states.

Dashboard data remains authoritative in `DashboardViewModel`. Navigation to Dashboard stays inside the React shell. Navigation to API configuration, configuration editor, MCP servers, Skills, installer, or environment check asks the WPF host to select the existing native view and reveal the native shell. No business command or persistence format is duplicated in JavaScript.

## Host architecture

- `MainWindow` keeps the existing native WPF shell intact as `NativeShell`.
- A `WebShellHost` layer contains WebView2 and starts hidden.
- On load, the host initializes WebView2, maps `https://appassets.claudeconsole` to the packaged `WebUI` directory, registers the message bridge, and navigates to `index.html`.
- React sends `app.ready` only after it can render. WPF then reveals the web shell.
- Initialization, navigation, missing assets, or script failures leave or restore the native shell and expose a concise fallback reason in debug output.
- The WebView2 CompositionControl is preferred because the current rounded WPF window uses transparency. If composition hosting is unavailable, the application remains fully usable through the native shell.

## Message protocol

Messages are JSON objects with `id`, `type`, and optional `payload`. Responses echo `id` and contain `ok`, optional `data`, and optional structured `error`.

Initial commands:

- `app.ready`: marks the React shell ready and returns current app state.
- `dashboard.get`: refreshes the existing dashboard view model and returns a serializable snapshot.
- `navigation.select`: accepts a stable panel key. Dashboard remains in Web UI; other known keys navigate the existing WPF view and request native fallback.
- `theme.get`: returns resolved light/dark mode, selected mode, and accent color.

Unknown commands and malformed JSON return structured errors and never crash the host.

## React structure

- `AppShell`: ambient background and responsive two-column layout.
- `GlassNavigation`: product identity, grouped navigation, selection, and native-page handoff.
- `GlassToolbar`: page title, refresh state, runtime indicator, and native UI escape hatch.
- `DashboardPage`: status overview, quick actions, system health, and recent events.
- `GlassSurface`: one wrapper around `LiquidGlass` with `subtle`, `interactive`, `prominent`, and `navigation` presets.
- `webViewBridge`: typed request/response client with a browser-preview fallback.

The background uses restrained static gradients and a subtle grid. Glass is limited to navigation, toolbar, primary dashboard regions, and interactive actions; list rows use low-cost CSS surfaces.

## Theme and visual behavior

The host provides resolved theme and accent values. React applies them as CSS variables and updates them when WPF appearance changes. All surfaces use continuous rounded corners; no white transition overlay is rendered by React when dark or system theme changes.

`liquid-glass-react` uses standard or prominent modes only. Shader mode is excluded from the first slice because the package documents it as less stable. When `prefers-reduced-motion` is enabled, WebView2 lacks required filter support, or the user agent reports software rendering constraints, the wrapper renders a CSS fallback with translucent fill, border, shadow, and `backdrop-filter` where available.

## Accessibility and responsiveness

The shell supports keyboard navigation, visible `focus-visible` outlines, labels for icon actions, semantic status text, and no hover-only workflow. At narrower desktop widths the sidebar contracts, summary cards collapse from four to two and then one column, and lower-priority descriptions hide before controls overflow.

## Verification

- xUnit covers protocol parsing, dashboard snapshot mapping, known navigation, theme payload, and unknown command errors.
- Vitest covers glass preset selection, browser fallback bridge behavior, and dashboard rendering.
- Vite production build is copied to the WPF output directory.
- Full .NET tests and Release build must pass.
- Manual verification checks startup, dashboard refresh, native-page handoff, theme switching, narrow window behavior, and forced missing-WebUI fallback.

## Risks

- WebView2 Runtime is an external Windows prerequisite. Missing runtime must not prevent startup.
- WPF transparent-window composition is sensitive to WebView2 control type and GPU drivers. CompositionControl plus native fallback contains this risk.
- The library relies on browser filter/SVG behavior. Edge/WebView2 is the supported target, but fallback styling remains mandatory.
- The first slice intentionally does not migrate complex edit forms, dialogs, long lists, or file editors.
