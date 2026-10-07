using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace CharmsBarPort
{
    /// <summary>
    /// Value of DWMWA_SYSTEMBACKDROP_TYPE (DWM_SYSTEMBACKDROP_TYPE).
    /// </summary>
    public enum BackdropType
    {
        Auto = 0,        // DWMSBT_AUTO
        None = 1,        // DWMSBT_NONE
        Mica = 2,        // DWMSBT_MAINWINDOW
        Acrylic = 3,     // DWMSBT_TRANSIENTWINDOW
        MicaAlt = 4      // DWMSBT_TABBEDWINDOW
    }

    /// <summary>
    /// Small native DWM helper: real Windows 11 system backdrops (Mica/Acrylic) on a normal,
    /// non-layered HWND. No third-party library, no fake gradients.
    /// </summary>
    public static class Backdrop
    {
        /// <summary>Backdrop used by every shell window. Switch to BackdropType.Acrylic to try Acrylic.</summary>
        public static BackdropType Default = BackdropType.Mica;

        // DwmSetWindowAttribute ids
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        private const int DWMWCP_DONOTROUND = 1;
        private const uint DWMWA_COLOR_NONE = 0xFFFFFFFE;

        private const int MinBuildForSystemBackdrop = 22621; // Windows 11 22H2

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref uint attrValue, int attrSize);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

        [DllImport("dwmapi.dll")]
        private static extern int DwmIsCompositionEnabled(out bool enabled);

        /// <summary>True when the OS supports DWMWA_SYSTEMBACKDROP_TYPE (Windows 11 22H2+).</summary>
        public static bool IsSupported { get; } = Environment.OSVersion.Version.Major >= 10 &&
                                                  Environment.OSVersion.Version.Build >= MinBuildForSystemBackdrop;

        /// <summary>
        /// True when the real system backdrop is (or will be) rendered: supported OS, DWM composition
        /// on, and no High Contrast. (With "Transparency effects" off, DWM itself swaps Mica for its
        /// solid fallback colour, so that case needs no handling here.)
        /// Re-evaluated on every call because the user can change these while the app runs.
        /// </summary>
        public static bool IsActive
        {
            get
            {
                if (!IsSupported || SystemParameters.HighContrast) return false;
                try { DwmIsCompositionEnabled(out bool on); return on; } catch { return false; }
            }
        }

        /// <summary>Forget the cached light/dark value (call after a user-preference change).</summary>
        public static void InvalidateThemeCache()
        {
            _themeStamp = DateTime.MinValue;
        }

        // Light/dark theme is polled by the existing 15 ms timers, so cache the registry reads.
        private static DateTime _themeStamp = DateTime.MinValue;
        private static bool _isLight;

        /// <summary>
        /// True when Windows uses the light theme. The shell surfaces (taskbar, flyouts) follow
        /// SystemUsesLightTheme, so that is what the charms follow too.
        /// </summary>
        public static bool IsLightTheme
        {
            get
            {
                if ((DateTime.UtcNow - _themeStamp).TotalMilliseconds > 500)
                {
                    _themeStamp = DateTime.UtcNow;
                    try
                    {
                        using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
                        {
                            object v = key?.GetValue("SystemUsesLightTheme");
                            _isLight = v != null && Convert.ToInt32(v) != 0;
                        }
                    }
                    catch { _isLight = false; }
                }
                return _isLight;
            }
        }

        /// <summary>
        /// Prepares the HWND for DWM composition and applies the backdrop. Call from SourceInitialized
        /// (HWND exists, window not yet shown). Safe to call again to refresh after a theme change.
        /// </summary>
        /// <param name="roundedCorners">false = square corners (windows docked to a screen edge).</param>
        public static void Apply(Window window, BackdropType type, bool roundedCorners)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            HwndSource source = HwndSource.FromHwnd(hwnd);

            bool mica = IsActive && type != BackdropType.None;

            // The WPF client area must be transparent (alpha 0) for the DWM material to show through.
            // In High Contrast the surface is a solid system colour instead.
            if (source?.CompositionTarget != null)
            {
                source.CompositionTarget.BackgroundColor = SystemParameters.HighContrast
                    ? SystemColors.WindowColor
                    : Colors.Transparent;
            }

            // Extending the frame over the whole client area is what lets per-pixel alpha (and the
            // system backdrop) show through a normal, non-layered window. Skipped in High Contrast
            // and when DWM composition is off, where the solid fallback backgrounds are used instead.
            bool extend = !SystemParameters.HighContrast;
            try { DwmIsCompositionEnabled(out bool comp); extend &= comp; } catch { extend = false; }

            var margins = extend
                ? new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 }
                : new MARGINS();
            DwmExtendFrameIntoClientArea(hwnd, ref margins);

            if (!IsSupported) return;

            int dark = IsLightTheme ? 0 : 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

            int corner = roundedCorners ? 0 : DWMWCP_DONOTROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

            // No 1px accent border: the charms are a flat, edge-docked surface.
            uint noBorder = DWMWA_COLOR_NONE;
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref noBorder, sizeof(uint));

            int backdrop = (int)(mica ? type : BackdropType.None);
            DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
        }
    }
}
