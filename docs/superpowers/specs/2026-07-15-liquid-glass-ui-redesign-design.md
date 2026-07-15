# Claude Console Liquid Glass UI Redesign

## Product and technical context

Claude Console is a .NET 9 WPF desktop application using CommunityToolkit.Mvvm and dependency injection. Navigation is implemented by swapping singleton view models through `MainViewModel.CurrentViewModel` and WPF data templates. There is no React, TypeScript, browser router, npm build, ESLint, or web runtime in the product.

The `rdev/liquid-glass-react` package cannot run inside WPF. The redesign therefore ports its visual principles: restrained refraction, edge highlights, saturation-aware translucent surfaces, elastic interaction feedback, large continuous radii, and graceful fallback behavior.

## Existing product map

- Dashboard: Claude CLI, API, Skills, MCP status and recent events.
- API configuration: provider selection, API key, connection testing, model detection, advanced options, save state.
- Configuration editor: file tabs, text editor, external-change conflict handling, backup-aware save.
- MCP manager: server list, connection tests, add/edit/rename/delete flows.
- Skills: installed and marketplace tabs, search, refresh, install and uninstall flows.
- Installer: CLI state, install/uninstall progress and output.
- Environment check: Node.js, npm and Git dependency state with download actions.

All existing view models, commands, services, models and persistence formats remain unchanged.

## Current UX issues

- The shell has a title bar and sidebar but no consistent contextual toolbar or responsive layout state.
- The `980px` minimum window width prevents useful narrow-window behavior.
- Page padding, card radii, surface colors and interaction timing are partly hard-coded across XAML and code-behind.
- Glass variants use inconsistent naming and some controls calculate their own colors instead of using shared tokens.
- Several pages use the same card grid regardless of task type, weakening information hierarchy.
- Focus, reduced-motion and low-performance behavior are not governed by one policy.
- Large translucent surfaces and decorative overlays can muddy the background in dark mode.

## New application shell

- A rounded custom window frame with a calm two-layer ambient background.
- A compact title bar for window controls and product identity.
- A collapsible left navigation rail containing workspace context, grouped navigation, appearance controls, language and version/update state.
- A contextual top toolbar showing the active page title, description, state and relevant shell actions.
- A main content canvas with adaptive padding and width.
- Update notifications appear as a compact global banner below the toolbar.

At wide widths the sidebar is expanded. At laptop widths it becomes narrower. At compact widths it switches to an icon-forward rail and page layouts collapse to one column.

## Design system

Theme-independent resources define spacing, typography, radii, control heights, motion durations, z-index values and layout dimensions. Light and dark theme dictionaries define window, surface, glass, text, border, accent and semantic status colors.

Glass presets:

- `Subtle`: toolbars and passive grouping surfaces.
- `Interactive`: buttons, selectable cards and navigation items.
- `Prominent`: dialogs, update banners and primary task surfaces.
- `Navigation`: sidebar and navigation groups.
- `Fallback`: opaque, low-cost surface for reduced effects or unsupported composition.

The WPF implementation uses layered gradients, luminous top/left rims, a darker lower edge, controlled shadows and small transform/opacity animations. It does not use expensive blur effects on long lists.

## Page layouts

- Dashboard: responsive summary tiles, an environment health strip and a grouped recent-event list.
- API configuration: provider/workspace rail plus a focused configuration form; collapses to one column.
- Configuration editor: file navigation and editor workspace; conflict UI remains prominent and modal.
- MCP: list and editor/detail workflow with low-cost list rows rather than high-cost glass on every nested element.
- Skills: segmented toolbar, search and adaptive card/list content with clear loading and empty states.
- Installer: task-centric status, progress, output and primary action grouping.
- Environment check: dependency checklist with one prominent remediation surface.

## Interaction and accessibility

- Hover, pressed, selected, disabled, loading, success, warning and error states combine color with icon, text, shape or motion.
- Keyboard focus uses a visible accent outline.
- Page transitions use opacity and transform only and are disabled when reduced motion is active.
- Dialogs retain explicit close/cancel actions and existing confirmation behavior.
- Minimum hit targets remain 36px, body text remains at least 14px for primary content, and semantic colors retain readable contrast.

## Performance and compatibility

- No React or npm dependency is added because it is incompatible with WPF.
- Windows 11 uses the existing Mica integration when available; Windows 10 and composition failures use theme-appropriate opaque surfaces.
- A UI performance policy disables ripple, elastic restore and page movement for reduced-motion, remote-session or low-render-tier environments.
- Long lists use low-cost solid/translucent rows; prominent glass is limited to shell and task boundaries.

## Verification

- Unit tests cover layout breakpoints, glass preset values and performance policy decisions.
- Existing view-model and service tests remain unchanged.
- Verification runs the full test suite and Release build.
