using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;

namespace CharmsBarPort
{
    /// <summary>
    /// Base class of the three charms windows (bar, clock, menu).
    ///
    /// The original code made these windows "disappear" by setting Window.Opacity to 0. That only works for
    /// WPF layered windows (AllowsTransparency="True"), which cannot host a DWM system backdrop.
    ///
    /// This class keeps the old call sites working (they still say "Opacity = x", "Show()", "Hide()") but
    /// maps them onto a normal, DWM-composed HWND:
    ///   * Opacity       -> the fade level of the window *content* (the HWND is never made layered);
    ///   * level reaches 0 -> the HWND is hidden (so it cannot swallow mouse input or draw an empty
    ///                      Mica rectangle), level rises above 0 -> it is shown again without activation;
    ///   * the HWND and its backdrop are created once and kept alive across hide/show.
    /// </summary>
    public class ShellWindow : Window
    {
        /// <summary>Fade levels at or below this are treated as fully invisible (below 1/255 alpha).</summary>
        private const double VisibleThreshold = 0.003;

        private double level = 1.0;        // value last written through Opacity (raw, like Window.Opacity was)
        private bool wanted;               // Show() requested / HWND created and not Hide()-den
        private bool forceShown;           // Activate() needs a visible HWND even while the level is 0
        private bool syncing;
        private double appliedContentOpacity = -1;
        private double startupLeft = double.NaN; // see ShellWindow_SourceInitialized

        /// <summary>Docked-to-edge windows keep square corners; floating ones may override this.</summary>
        protected virtual bool RoundedCorners => false;

        public ShellWindow()
        {
            // Showing a charm must never take focus away from the application underneath.
            ShowActivated = false;

            SourceInitialized += ShellWindow_SourceInitialized;
            Loaded += (s, e) =>
            {
                if (!double.IsNaN(startupLeft)) { Left = startupLeft; startupLeft = double.NaN; }
                SyncShellVisibility();
            };
        }

        // ------------------------------------------------------------------ visibility / lifecycle

        /// <summary>
        /// Legacy "window opacity". Setting it never changes the HWND's own alpha; see class comment.
        /// </summary>
        public new double Opacity
        {
            get { return level; }
            set
            {
                level = value;
                forceShown = false;
                SyncShellVisibility();
            }
        }

        public new void Show()
        {
            wanted = true;
            SyncShellVisibility();
        }

        public new void Hide()
        {
            wanted = false;
            forceShown = false;
            SyncShellVisibility();
        }

        /// <summary>
        /// Activation for the "Win+C" entry path: the bar is still hidden by its fade level at that moment, and a
        /// hidden HWND cannot be activated, so it is shown first (without activation) and then activated.
        /// The very next Opacity write (the same timer tick) takes over the visibility again.
        /// Ordinary Activate() calls are left untouched on purpose.
        /// </summary>
        protected bool ActivateShell()
        {
            wanted = true;
            if (level <= VisibleThreshold) forceShown = true;
            SyncShellVisibility();
            return base.Activate();
        }

        /// <summary>
        /// Single place that reconciles the logical state (wanted / level) with the real HWND state.
        /// Cheap and idempotent: the existing 15 ms timer calls it indirectly many times per second.
        /// </summary>
        protected void SyncShellVisibility()
        {
            if (syncing) return;
            syncing = true;
            try
            {
                bool visible = wanted && (level > VisibleThreshold || forceShown);

                if (visible && !IsVisible)
                {
                    base.Show();            // ShowActivated = false -> SW_SHOWNOACTIVATE
                }
                else if (!visible && IsVisible)
                {
                    base.Hide();            // HWND (and its backdrop) stay alive
                }

                UIElement root = Content as UIElement;
                if (root != null)
                {
                    double c = level < 0 ? 0 : (level > 1 ? 1 : level);
                    if (c != appliedContentOpacity)
                    {
                        appliedContentOpacity = c;
                        root.Opacity = c;
                    }
                }
            }
            finally
            {
                syncing = false;
            }
        }

        // ------------------------------------------------------------------ backdrop

        private void ShellWindow_SourceInitialized(object sender, EventArgs e)
        {
            // The HWND exists now. If somebody (e.g. StartupUri) is showing us, remember that.
            wanted = true;

            // A window that WPF shows on its own (StartupUri) while logically hidden would flash an empty Mica
            // rectangle for a frame or two before Loaded hides it. Park it off-screen until then.
            if (level <= VisibleThreshold && !forceShown)
            {
                startupLeft = Left;
                Left = -32000;
            }

            ApplyBackdrop();
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        }

        /// <summary>(Re)applies the DWM backdrop, dark/light mode, corner and frame settings.</summary>
        protected void ApplyBackdrop()
        {
            Backdrop.Apply(this, Backdrop.Default, RoundedCorners);
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.Color &&
                e.Category != UserPreferenceCategory.General &&
                e.Category != UserPreferenceCategory.VisualStyle &&
                e.Category != UserPreferenceCategory.Accessibility)
            {
                return;
            }

            // Raised on the SystemEvents thread.
            Dispatcher.BeginInvoke((Action)(() =>
            {
                Backdrop.InvalidateThemeCache();
                if (new WindowInteropHelper(this).Handle != IntPtr.Zero) ApplyBackdrop();
            }), DispatcherPriority.Normal);
        }

        protected override void OnClosed(EventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            base.OnClosed(e);
        }
    }
}
