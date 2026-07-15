using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using ClaudeCodePanel.Windows.Helpers;
using ClaudeCodePanel.Windows.Design;
using ClaudeCodePanel.Windows.Services;
using ClaudeCodePanel.Windows.ViewModels;
using ClaudeCodePanel.Windows.Views.Sidebar;
using ClaudeCodePanel.Windows.WebUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;

namespace ClaudeCodePanel.Windows.Views
{
    /// <summary>
    /// Main application window with custom chrome, Mica backdrop, a 32 px title bar,
    /// sidebar navigation, and a content area driven by the selected panel ViewModel.
    /// </summary>
    public partial class MainWindow : Window
    {
        private const double TitleBarCaptureHeight = 52;

        // ── P/Invoke for edge resize with WindowStyle=None ──────────────────

        private const int WM_NCHITTEST = 0x0084;
        private const int WM_GETMINMAXINFO = 0x0024;

        // Mouse-position-in-window-to-resize-direction map values
        private const int HTLEFT   = 10;
        private const int HTRIGHT  = 11;
        private const int HTTOP    = 12;
        private const int HTTOPLEFT     = 13;
        private const int HTTOPRIGHT    = 14;
        private const int HTBOTTOM      = 15;
        private const int HTBOTTOMLEFT  = 16;
        private const int HTBOTTOMRIGHT = 17;

        // Edge resize border thickness in device-independent pixels
        private const int ResizeBorderThickness = 4;

        // ── AllowsTransparency maximize compensation ─────────────────────
        // When AllowsTransparency=True, WPF uses layered windows which don't
        // properly fill the working area when maximized — gaps appear on
        // the right/bottom edges. We compensate by oversizing the maximized
        // window by this many pixels on each side.
        private const int MaximizeOffset = 8;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        // ── Drag region state ────────────────────────────────────────────────

        private readonly MainViewModel _mainViewModel;
        private readonly DashboardViewModel _dashboardViewModel;
        private readonly WebUiBridge _webUiBridge;
        private PropertyChangedEventHandler? _themeServicePropertyChangedHandler;
        private bool _webShellInitialized;
        private bool _webShellReady;
        private bool _useWebShell = true;

        private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        // ── Constructor ──────────────────────────────────────────────────────

        public MainWindow(MainViewModel mainViewModel)
        {
            InitializeComponent();

            DataContext = mainViewModel;
            _mainViewModel = mainViewModel;
            _dashboardViewModel = App.Services.GetRequiredService<DashboardViewModel>();
            _webUiBridge = new WebUiBridge(
                GetDashboardSummaryAsync,
                CreateThemeSnapshot,
                NavigateFromWeb,
                ShowNativeShell);

            // Set initial content without animation (first load)
            ContentArea.Content = mainViewModel.SelectedPanelViewModel;

            // Animate content transitions when SelectedPanelViewModel changes
            mainViewModel.PropertyChanged += OnSelectedPanelViewModelChanged;

            // Keep the maximize/restore button glyph in sync with window state.
            StateChanged += OnWindowStateChanged;
            SizeChanged += OnWindowSizeChanged;
            Closed += OnClosed;
        }

        // ── Source Initialized (Mica + edge resize hook) ──────────────────────

        /// <summary>
        /// Applies Mica backdrop, dark title bar, and hooks WndProc for edge-resize.
        /// Called BEFORE Show() so AllowsTransparency can be set safely.
        /// </summary>
        private void OnSourceInitialized(object sender, EventArgs e)
        {
            // Mica and dark title bar — must happen before Show()
            Windows11Interop.EnableMica(this);
            Windows11Interop.ApplyTitleBarTheme(this, ThemeService.Instance.IsDarkTheme);

            // Edge-resize WndProc hook
            var handle = new WindowInteropHelper(this).Handle;
            var source = HwndSource.FromHwnd(handle);

            if (source != null)
            {
                source.AddHook(WndProc);
            }

            Loaded += OnMainWindowLoaded;
        }

        private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnMainWindowLoaded;
            _themeServicePropertyChangedHandler ??= OnThemeServicePropertyChanged;
            ThemeService.Instance.PropertyChanged -= _themeServicePropertyChangedHandler;
            ThemeService.Instance.PropertyChanged += _themeServicePropertyChangedHandler;

            _ = InitializeWebShellAsync();
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _mainViewModel.PropertyChanged -= OnSelectedPanelViewModelChanged;
            StateChanged -= OnWindowStateChanged;
            SizeChanged -= OnWindowSizeChanged;

            if (_themeServicePropertyChangedHandler != null)
                ThemeService.Instance.PropertyChanged -= _themeServicePropertyChangedHandler;

            if (WebShellView.CoreWebView2 != null)
            {
                WebShellView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                WebShellView.CoreWebView2.ProcessFailed -= OnWebProcessFailed;
                WebShellView.CoreWebView2.NavigationCompleted -= OnWebNavigationCompleted;
            }

            WebShellView.Dispose();

            Closed -= OnClosed;
        }

        private async void OnThemeServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ThemeService.AppearanceRevision))
                return;

            Dispatcher.Invoke(() =>
            {
                Windows11Interop.ApplyTitleBarTheme(this, ThemeService.Instance.IsDarkTheme);
            });

            await NotifyWebThemeChangedAsync();

            // Force WPF to destroy and recreate the current View so all
            // DynamicResource are re-resolved against the latest appearance state.
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            var current = _mainViewModel.SelectedPanelViewModel;
            ContentArea.Content = null;
            ContentArea.Content = current;
        }

        private async Task InitializeWebShellAsync()
        {
            if (_webShellInitialized)
                return;

            _webShellInitialized = true;
            var assetDirectory = WebUiAssetLocator.GetAssetDirectory(AppContext.BaseDirectory);
            if (!WebUiAssetLocator.IsReady(assetDirectory))
            {
                ShowNativeShell();
                Debug.WriteLine($"[WebUI] Assets not found: {assetDirectory}");
                return;
            }

            try
            {
                WebShellView.IsHitTestVisible = false;
                WebShellView.DefaultBackgroundColor = ThemeService.Instance.IsDarkTheme
                    ? System.Drawing.Color.FromArgb(0x07, 0x10, 0x1C)
                    : System.Drawing.Color.FromArgb(0xED, 0xF4, 0xF8);

                await WebShellView.EnsureCoreWebView2Async();
                var core = WebShellView.CoreWebView2;
                core.SetVirtualHostNameToFolderMapping(
                    "appassets.claudeconsole",
                    assetDirectory,
                    CoreWebView2HostResourceAccessKind.DenyCors);
                core.Settings.AreDevToolsEnabled = Debugger.IsAttached;
                core.Settings.AreDefaultContextMenusEnabled = Debugger.IsAttached;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsZoomControlEnabled = false;
                core.WebMessageReceived += OnWebMessageReceived;
                core.ProcessFailed += OnWebProcessFailed;
                core.NavigationCompleted += OnWebNavigationCompleted;
                core.Navigate("https://appassets.claudeconsole/index.html");
            }
            catch (Exception ex)
            {
                ShowNativeShell();
                Debug.WriteLine($"[WebUI] Initialization failed: {ex}");
            }
        }

        private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var response = await _webUiBridge.HandleAsync(e.WebMessageAsJson);
                WebShellView.CoreWebView2?.PostWebMessageAsJson(
                    JsonSerializer.Serialize(response, WebJsonOptions));

                using var messageDocument = JsonDocument.Parse(e.WebMessageAsJson);
                if (messageDocument.RootElement.TryGetProperty("type", out var typeElement) &&
                    string.Equals(typeElement.GetString(), "app.ready", StringComparison.Ordinal))
                {
                    _webShellReady = response.Ok;
                    if (_webShellReady && _useWebShell)
                        ShowWebShell();
                }
            }
            catch (Exception ex)
            {
                ShowNativeShell();
                Debug.WriteLine($"[WebUI] Message handling failed: {ex}");
            }
        }

        private void OnWebProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
        {
            ShowNativeShell();
            Debug.WriteLine($"[WebUI] Process failed: {e.ProcessFailedKind} {e.Reason}");
        }

        private void OnWebNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
                return;

            ShowNativeShell();
            Debug.WriteLine($"[WebUI] Navigation failed: {e.WebErrorStatus}");
        }

        private async Task<Models.DashboardSummary> GetDashboardSummaryAsync()
        {
            await _dashboardViewModel.LoadSummaryAsync();
            return _dashboardViewModel.Summary;
        }

        private static ThemeSnapshot CreateThemeSnapshot()
        {
            var theme = ThemeService.Instance;
            var accent = theme.ActiveAccentColor;
            return new ThemeSnapshot(
                theme.CurrentThemeMode.ToString().ToLowerInvariant(),
                theme.IsDarkTheme,
                $"#{accent.R:X2}{accent.G:X2}{accent.B:X2}");
        }

        private void NavigateFromWeb(MainPanelType panel)
        {
            Dispatcher.Invoke(() =>
            {
                _mainViewModel.Navigate(panel);
            });
        }

        private void ShowWebShell()
        {
            if (!_webShellReady)
                return;

            _useWebShell = true;
            NativeWorkspace.Visibility = Visibility.Collapsed;
            WebShellHost.Visibility = Visibility.Visible;
            WebShellView.IsHitTestVisible = true;
            ShellToggleButton.Content = "原生界面";
        }

        private void ShowNativeShell()
        {
            Dispatcher.Invoke(() =>
            {
                _useWebShell = false;
                WebShellView.IsHitTestVisible = false;
                WebShellHost.Visibility = Visibility.Collapsed;
                NativeWorkspace.Visibility = Visibility.Visible;
                ShellToggleButton.Content = "Liquid Glass";
            });
        }

        private async Task NotifyWebThemeChangedAsync()
        {
            if (!_webShellReady || WebShellView.CoreWebView2 == null)
                return;

            var theme = CreateThemeSnapshot();
            WebShellView.DefaultBackgroundColor = theme.IsDark
                ? System.Drawing.Color.FromArgb(0x07, 0x10, 0x1C)
                : System.Drawing.Color.FromArgb(0xED, 0xF4, 0xF8);
            await WebShellView.CoreWebView2.ExecuteScriptAsync(
                $"window.dispatchEvent(new CustomEvent('claudeconsole:theme', {{ detail: {JsonSerializer.Serialize(theme, WebJsonOptions)} }}));");
        }

        private void OnShellToggleClick(object sender, RoutedEventArgs e)
        {
            if (_useWebShell)
            {
                ShowNativeShell();
                return;
            }

            _useWebShell = true;
            if (_webShellReady)
            {
                _mainViewModel.Navigate(MainPanelType.Dashboard);
                ShowWebShell();
            }
            else
            {
                _ = InitializeWebShellAsync();
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_NCHITTEST:
                    // Only handle hit-testing when the mouse is over the content area
                    // (i.e. not over a button or other interactive element where
                    // LParamToClient returns null because those elements have their
                    // own hit-test results).
                    if (ResizeMode == ResizeMode.CanResizeWithGrip
                        || ResizeMode == ResizeMode.CanResize)
                    {
                        var result = HitTestNCA(lParam);
                        if (result != 0)
                        {
                            handled = true;
                            return new IntPtr(result);
                        }
                    }
                    break;

                case WM_GETMINMAXINFO:
                    ConstrainMaxSizeToWorkingArea(hwnd, lParam);
                    handled = true;
                    return IntPtr.Zero;
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Determines the non-client-area hit-test value for edge resizing.
        /// Returns the appropriate HT* constant or 0 to let WPF handle it.
        /// </summary>
        private int HitTestNCA(IntPtr lParam)
        {
            // Only process when the window is in Normal state (not maximized).
            if (WindowState != WindowState.Normal)
                return 0;

            var mouseScreen = new Point(
                (int)(lParam.ToInt64() & 0xFFFF),
                (int)((lParam.ToInt64() >> 16) & 0xFFFF));

            var rect = new Rect(Left, Top, ActualWidth, ActualHeight);

            // Skip if mouse is completely outside the window bounds.
            if (!rect.Contains(mouseScreen))
                return 0;

            var mouseLocal = new Point(
                mouseScreen.X - rect.Left,
                mouseScreen.Y - rect.Top);

            bool left   = mouseLocal.X <= ResizeBorderThickness;
            bool right  = mouseLocal.X >= rect.Width  - ResizeBorderThickness;
            bool top    = mouseLocal.Y <= ResizeBorderThickness;
            bool bottom = mouseLocal.Y >= rect.Height - ResizeBorderThickness;

            if (top && left)       return HTTOPLEFT;
            if (top && right)      return HTTOPRIGHT;
            if (bottom && left)    return HTBOTTOMLEFT;
            if (bottom && right)   return HTBOTTOMRIGHT;
            if (left)              return HTLEFT;
            if (right)             return HTRIGHT;
            if (top)               return HTTOP;
            if (bottom)            return HTBOTTOM;

            return 0; // Not on a resize edge — let WPF decide.
        }

        /// <summary>
        /// Adjusts the maximize size to the working area of the monitor.
        /// When AllowsTransparency=True, WPF uses layered windows that render
        /// the client area ~7px inset from the window rect, leaving a visible
        /// gap. We compensate by oversizing the maximized window so the inset
        /// region falls outside the screen and the visible content fills the
        /// entire working area edge-to-edge.
        /// </summary>
        private static void ConstrainMaxSizeToWorkingArea(IntPtr hwnd, IntPtr lParam)
        {
            var hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (hMonitor == IntPtr.Zero)
                return;

            var monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = Marshal.SizeOf<MONITORINFO>();
            GetMonitorInfo(hMonitor, ref monitorInfo);

            var rc = monitorInfo.rcWork;

            var offset = MaximizeOffset;
            Marshal.WriteInt32(lParam, 0,  rc.Left - offset);                 // ptMaxPosition.x
            Marshal.WriteInt32(lParam, 4,  rc.Top - offset);                  // ptMaxPosition.y
            Marshal.WriteInt32(lParam, 8,  (rc.Right - rc.Left) + offset * 2); // ptMaxSize.x
            Marshal.WriteInt32(lParam, 12, (rc.Bottom - rc.Top) + offset * 2); // ptMaxSize.y
        }

        // ── Content transition ───────────────────────────────────────────────

        /// <summary>
        /// When the MainViewModel navigates to a different panel, animate the
        /// content transition: old panel exits (fade + slide up), new panel
        /// enters (fade + slide down).
        /// </summary>
        private async void OnSelectedPanelViewModelChanged(object? sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MainViewModel.SelectedPanelViewModel))
                return;

            var newContent = _mainViewModel.SelectedPanelViewModel;
            if (newContent == null)
                return;

            await ContentTransitionBehavior.TransitionToAsync(ContentArea, newContent);
        }

        // ── Title Bar ────────────────────────────────────────────────────────

        /// <summary>
        /// Initiates a window drag when the user clicks and drags the title bar.
        /// Only responds when the window is in Normal state; double-clicking
        /// in a maximized state toggles back to Normal.
        /// </summary>
        private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximizeRestore();
                return;
            }

            if (WindowState == WindowState.Maximized)
            {
                // If maximized, restore first, then drag.
                var mouseScreen = PointToScreen(e.GetPosition(this));

                WindowState = WindowState.Normal;

                // Reposition so the cursor stays at the same relative horizontal
                // position on the title bar.
                var ratio = mouseScreen.X / SystemParameters.PrimaryScreenWidth;
                var newLeft = mouseScreen.X - (ActualWidth * ratio);
                Left = newLeft;
                Top = 0;
            }

            DragMove();
        }

        // ── Window Buttons ───────────────────────────────────────────────────

        private void OnMinimizeClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
        {
            ToggleMaximizeRestore();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>Dismisses the update notification banner.</summary>
        private void OnDismissUpdateBanner(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.IsUpdateAvailable = false;
        }

        private void ToggleMaximizeRestore()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        // ── Window State Changed ────────────────────────────────────────────

        private void OnWindowStateChanged(object? sender, EventArgs e)
        {
            if (MaximizeRestoreButton == null)
                return;

            if (WindowState == WindowState.Maximized)
            {
                MaximizeRestoreButton.Content = "\xE923";  // Restore
                MaximizeRestoreButton.ToolTip = "Restore";

                // Remove rounded corners and shadow when maximized
                WindowChromeBorder.CornerRadius = new CornerRadius(0);
                WindowShadow.Visibility = Visibility.Collapsed;
            }
            else
            {
                MaximizeRestoreButton.Content = "\xE922";  // Maximize
                MaximizeRestoreButton.ToolTip = "Maximize";

                // Restore rounded corners and shadow
                WindowChromeBorder.CornerRadius = TryFindResource("RadiusWindow") is CornerRadius radius
                    ? radius
                    : new CornerRadius(26);
                WindowShadow.Visibility = Visibility.Visible;
            }
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyLayoutProfile(WindowLayoutProfile.ForWidth(e.NewSize.Width));
        }

        private void ApplyLayoutProfile(WindowLayoutProfile profile)
        {
            SidebarColumn.Width = new GridLength(profile.SidebarWidth);
            SidebarHost.Margin = profile.Mode == WindowLayoutMode.Compact
                ? new Thickness(0, 0, 10, 0)
                : new Thickness(0, 0, 16, 0);
            PageDescriptionText.Visibility = profile.Mode == WindowLayoutMode.Compact
                ? Visibility.Collapsed
                : Visibility.Visible;
            SidebarView.SetCompactMode(!profile.ShowNavigationLabels);
        }
    }
}
