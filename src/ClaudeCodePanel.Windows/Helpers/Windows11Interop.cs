using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace ClaudeCodePanel.Windows.Helpers
{
    /// <summary>
    /// Provides P/Invoke helpers for Windows 11-specific window features:
    /// Mica backdrop material and dark title bar styling.
    /// </summary>
    public static class Windows11Interop
    {
        // ----------------------------------------------------------------
        // DwmSetWindowAttribute constants
        // ----------------------------------------------------------------

        /// <summary>Enables the Mica backdrop material on Windows 11 (build 22000+).</summary>
        private const int DWMWA_MICA = 1029;

        /// <summary>
        /// Controls whether the title bar uses the dark/light immersive mode.
        /// 20 = DWMWA_USE_IMMERSIVE_DARK_MODE on Windows 10 1903+/Windows 11.
        /// </summary>
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        // ----------------------------------------------------------------
        // Version detection
        // ----------------------------------------------------------------

        /// <summary>Windows 11 first public build (21H2).</summary>
        private static readonly Version Windows11MinVersion = new Version(10, 0, 22000);

        /// <summary>
        /// True when running on Windows 11 (build ≥ 10.0.22000).
        /// Uses RuntimeInformation to confirm Windows, then OSVersion for build.
        /// </summary>
        private static bool IsWindows11OrGreater =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            Environment.OSVersion.Version >= Windows11MinVersion;

        // ----------------------------------------------------------------
        // P/Invoke: dwmapi.dll
        // ----------------------------------------------------------------

        /// <summary>
        /// Sets a Desktop Window Manager (DWM) attribute on a window handle.
        /// </summary>
        /// <param name="hwnd">Handle to the window.</param>
        /// <param name="attr">The DWM attribute to set (e.g., DWMWA_MICA).</param>
        /// <param name="attrValue">Pointer to the new attribute value.</param>
        /// <param name="attrSize">Size of the value, in bytes.</param>
        /// <returns>S_OK (0) on success; otherwise an HRESULT error code.</returns>
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attr,
            ref int attrValue,
            int attrSize);

        // ----------------------------------------------------------------
        // Public API
        // ----------------------------------------------------------------

        /// <summary>
        /// Enables the Mica backdrop material on a WPF window when running
        /// on Windows 11 (build 10.0.22000 or later).  Falls back to a dark
        /// solid background (#080d1f) on older Windows versions.
        /// </summary>
        /// <remarks>
        /// Mica requires <c>WindowStyle="None"</c> and
        /// <c>AllowsTransparency="True"</c>.  This method stamps those
        /// properties automatically, but callers should also ensure the
        /// window content provides its own title-bar chrome (drag region,
        /// min/max/close buttons).
        /// </remarks>
        public static void EnableMica(Window window)
        {
            if (window is null)
                throw new ArgumentNullException(nameof(window));

            // Resolve the HWND once the window is loaded (SourceInitialized
            // is the earliest safe point for WPF interop handles).
            void OnSourceInitialized(object? sender, EventArgs e)
            {
                window.SourceInitialized -= OnSourceInitialized;

                if (IsWindows11OrGreater)
                    EnableMicaInternal(window);
                else
                    ApplyFallbackBackground(window);
            }

            // If the window is already initialized, run immediately;
            // otherwise subscribe.
            if (window.IsInitialized)
            {
                OnSourceInitialized(window, EventArgs.Empty);
            }
            else
            {
                window.SourceInitialized += OnSourceInitialized;
            }
        }

        /// <summary>
        /// Applies a dark or light immersive title bar to the specified WPF
        /// window using DWMWA_USE_IMMERSIVE_DARK_MODE (attribute 20).
        /// </summary>
        public static void ApplyTitleBarTheme(Window window, bool dark)
        {
            if (window is null)
                throw new ArgumentNullException(nameof(window));

            void OnSourceInitialized(object? sender, EventArgs e)
            {
                window.SourceInitialized -= OnSourceInitialized;
                SetTitleBarTheme(window, dark);
            }

            if (window.IsInitialized)
            {
                OnSourceInitialized(window, EventArgs.Empty);
            }
            else
            {
                window.SourceInitialized += OnSourceInitialized;
            }
        }

        public static void ApplyDarkTitleBar(Window window)
        {
            ApplyTitleBarTheme(window, dark: true);
        }

        // ----------------------------------------------------------------
        // Private helpers
        // ----------------------------------------------------------------

        private static void EnableMicaInternal(Window window)
        {
            // Mica requires a layered, non-client window — set only if not already set.
            if (window.WindowStyle != WindowStyle.None)
                window.WindowStyle = WindowStyle.None;
            if (!window.AllowsTransparency)
                window.AllowsTransparency = true;

            var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            SetTitleBarThemeInternal(hwnd, dark: true);

            // Enable the Mica backdrop.
            int useMica = 1; // BOOL TRUE
            DwmSetWindowAttribute(hwnd, DWMWA_MICA, ref useMica, sizeof(int));
        }

        private static void ApplyFallbackBackground(Window window)
        {
            window.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#080d1f"));
        }

        private static void SetTitleBarTheme(Window window, bool dark)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            SetTitleBarThemeInternal(hwnd, dark);
        }

        private static void SetTitleBarThemeInternal(IntPtr hwnd, bool dark)
        {
            int useDarkMode = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
        }
    }
}
