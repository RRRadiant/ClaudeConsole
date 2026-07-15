# Claude Console Liquid Glass UI Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the existing inconsistent WPF presentation layer with a responsive, accessible native Liquid Glass design system while preserving every business workflow.

**Architecture:** Keep services, models, commands and singleton view models intact. Add pure UI policy classes for layout, glass presets and performance fallbacks; consume them from a redesigned WPF application shell, shared controls, theme resources and page XAML.

**Tech Stack:** .NET 9, WPF, CommunityToolkit.Mvvm, XAML resource dictionaries, xUnit.

---

### Task 1: Test UI policies

**Files:**
- Create: `src/ClaudeCodePanel.Windows.Tests/UI/UiPolicyTests.cs`
- Create: `src/ClaudeCodePanel.Windows/Design/WindowLayoutProfile.cs`
- Create: `src/ClaudeCodePanel.Windows/Design/GlassPresetCatalog.cs`
- Create: `src/ClaudeCodePanel.Windows/Design/UiPerformancePolicy.cs`

- [ ] Write tests for wide, laptop and compact layout thresholds.
- [ ] Run the UI policy tests and confirm they fail because the production types do not exist.
- [ ] Implement immutable policy records and deterministic selectors.
- [ ] Run the UI policy tests and full test suite.

### Task 2: Establish design resources

**Files:**
- Create: `src/ClaudeCodePanel.Windows/Resources/Themes/DesignSystem.xaml`
- Modify: `src/ClaudeCodePanel.Windows/App.xaml`
- Modify: `src/ClaudeCodePanel.Windows/Resources/Themes/DarkTheme.xaml`
- Modify: `src/ClaudeCodePanel.Windows/Resources/Themes/LightTheme.xaml`

- [ ] Add shared resources for spacing, radii, typography, control sizes, layout widths, shadows and motion.
- [ ] Add balanced Liquid Glass surface, rim, hover, focus and fallback brushes to both themes.
- [ ] Load design resources before shared controls and theme dictionaries.
- [ ] Build to validate every dynamic resource reference.

### Task 3: Rebuild shared glass controls

**Files:**
- Modify: `src/ClaudeCodePanel.Windows/Views/Shared/GlassCard.xaml`
- Modify: `src/ClaudeCodePanel.Windows/Views/Shared/GlassCard.xaml.cs`
- Modify: `src/ClaudeCodePanel.Windows/Views/Shared/GlassButton.xaml`
- Modify: `src/ClaudeCodePanel.Windows/Views/Shared/GlassButton.xaml.cs`
- Modify: `src/ClaudeCodePanel.Windows/Views/Shared/GlassTextField.xaml`
- Modify: `src/ClaudeCodePanel.Windows/Views/Shared/SearchField.xaml`
- Modify: `src/ClaudeCodePanel.Windows/Views/Shared/UtilityViews.xaml`

- [ ] Map card variants to shared presets and remove hard-coded radii.
- [ ] Add visible keyboard focus and reduced-motion aware button feedback.
- [ ] Align inputs, search, headers, empty states and dividers with the same tokens.
- [ ] Build and run shared control tests.

### Task 4: Rebuild the application shell

**Files:**
- Modify: `src/ClaudeCodePanel.Windows/ViewModels/MainViewModel.cs`
- Modify: `src/ClaudeCodePanel.Windows/Views/MainWindow.xaml`
- Modify: `src/ClaudeCodePanel.Windows/Views/MainWindow.xaml.cs`
- Modify: `src/ClaudeCodePanel.Windows/Views/Sidebar/SidebarView.xaml`
- Modify: `src/ClaudeCodePanel.Windows/Views/Sidebar/SidebarView.xaml.cs`
- Modify: `src/ClaudeCodePanel.Windows/Helpers/ContentTransitionBehavior.cs`

- [ ] Expose active page metadata without changing navigation commands.
- [ ] Add contextual toolbar, adaptive content padding and sidebar collapse controls.
- [ ] Apply layout profiles on window size changes.
- [ ] Preserve the merged appearance controls and update notification workflow.
- [ ] Respect reduced motion for page transitions and interactive glass effects.

### Task 5: Redesign core pages

**Files:**
- Modify all XAML files under `src/ClaudeCodePanel.Windows/Views/Dashboard`, `API`, `Config`, `MCP`, `Skills`, `Installer` and `EnvCheck`.

- [ ] Replace fixed outer margins with adaptive page padding.
- [ ] Use task-specific layouts rather than one repeated card grid.
- [ ] Preserve every binding, command, named element and code-behind event contract.
- [ ] Normalize loading, empty, success, warning and error presentation.
- [ ] Collapse multi-column layouts at compact widths without horizontal scrolling.

### Task 6: Verify and audit

**Files:**
- Modify tests only if a genuine regression is identified.

- [ ] Run `dotnet test ClaudeCodePanel.Windows.sln`.
- [ ] Run `dotnet build ClaudeCodePanel.Windows.sln -c Release`.
- [ ] Search XAML for remaining hard-coded colors, radii and oversized fixed widths.
- [ ] Review the git diff for accidental business-logic changes and report residual risks.
