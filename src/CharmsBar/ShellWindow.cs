using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;

namespace CharmsBarPort
{
    /// <summary>
    /// Base class of the three charms windows (bar, clock, menu).
    ///
    /// Why it exists
    /// -------------
    /// 1. Visibility. The original code hid the windows by setting Window.Opacity = 0. That only works for WPF
    ///    layered windows (AllowsTransparency="True"), which cannot host a DWM system backdrop. Here the legacy
    ///    "Opacity = x", "Show()" and "Hide()" calls are kept, but mapped onto a normal DWM-composed HWND:
    ///      * Opacity  -> fade level of the window CONTENT (the HWND is never layered);
    ///      * level 0  -> the HWND is hidden, so it cannot swallow input or draw an empty backdrop;
    ///      * level >0 -> shown again WITHOUT activation; the HWND and its backdrop are created once and kept.
    ///
    /// 2. Activation. The original code called Activate()/Focus() all the time and read the cursor through
    ///    Mouse.Capture - that is what made the application underneath lose focus and eat its clicks.
    ///    The legacy state machine still needs an "activated" state ("the charms are in use"), so that is now a
    ///    LOGICAL state (pseudoActive) which never touches the OS focus. Real OS activation is only granted while
    ///    ActivationAllowed is true (keyboard mode of the bar). WM_MOUSEACTIVATE answers MA_NOACTIVATE otherwise, so
    ///    clicking a charm does not activate the window either - it still receives the mouse input.
    ///
    /// 3. Backdrop. See Backdrop.cs. Mica is drawn in its "active" look only for the active window, so the
    ///    non-client-active state is kept on (WM_NCACTIVATE) without activating the window.
    /// </summary>
    public class ShellWindow : Window
    {
        private const double VisibleThreshold = 0.003;   // below 1/255 alpha == invisible

        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int WM_NCACTIVATE = 0x0086;
        private const int MA_NOACTIVATE = 3;

        [DllImport("user32.dll")] private static extern IntPtr DefWindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

        private double level = 1.0;        // value last written through Opacity (raw, like Window.Opacity was)
        private bool wanted;               // Show() requested / HWND created and not Hide()-den
        private bool forceShown;           // keyboard activation needs a visible HWND even while the level is 0
        private bool syncing;
        private bool pseudoActive;         // logical "charms in use" activation; the OS focus is NOT involved
        private double appliedContentOpacity = -1;
        private double startupLeft = double.NaN;

        /// <summary>Docked-to-edge windows keep square corners; floating ones override this.</summary>
        protected virtual bool RoundedCorners => false;

        /// <summary>When false (default) this window never takes the OS focus, not even when clicked.</summary>
        protected virtual bool ActivationAllowed => false;

        public ShellWindow()
        {
            ShowActivated = false;   // showing a charm must never take focus from the application underneath

            SourceInitialized += ShellWindow_SourceInitialized;
            Loaded += (s, e) =>
            {
                if (!double.IsNaN(startupLeft)) { Left = startupLeft; startupLeft = double.NaN; }
                SyncShellVisibility();
            };
        }

        // ------------------------------------------------------------------ activation (logical vs. real)

        /// <summary>
        /// "Is the charms UI activated?" = really active (keyboard mode) OR logically activated by the hot-corner
        /// flow. Shadows Window.IsActive so the existing 5000-line state machine keeps working unchanged.
        /// </summary>
        public new bool IsActive
        {
            get { return base.IsActive || pseudoActive; }
        }

        public new bool Activate()
        {
            if (ActivationAllowed) return ActivateReal();

            // Hot-corner use: only the logical state changes. Never SetForegroundWindow, never focus.
            if (!pseudoActive && IsVisible)
            {
                pseudoActive = true;
                OnActivated(EventArgs.Empty);
            }
            return false;
        }

        public new bool Focus()
        {
            return ActivationAllowed ? base.Focus() : false;
        }

        /// <summary>Real OS activation for the keyboard shortcut. The bar is first shown if its level is still 0.</summary>
        protected bool ActivateShell()
        {
            wanted = true;
            if (level <= VisibleThreshold) forceShown = true;
            SyncShellVisibility();
            return ActivateReal();
        }

        private bool ActivateReal()
        {
            pseudoActive = false;
            bool ok = base.Activate();
            if (!base.IsActive)
            {
                // Windows refuses SetForegroundWindow from a background process; attach to the foreground
                // thread's input queue for the moment of the call (standard workaround).
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    uint pid;
                    IntPtr fg = GetForegroundWindow();
                    uint fgThread = fg == IntPtr.Zero ? 0 : GetWindowThreadProcessId(fg, out pid);
                    uint me = GetCurrentThreadId();
                    bool attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
                    SetForegroundWindow(hwnd);
                    if (attached) AttachThreadInput(me, fgThread, false);
                }
                ok = base.IsActive;
            }
            return ok;
        }

        // ------------------------------------------------------------------ visibility / lifecycle

        /// <summary>Legacy "window opacity". Never changes the HWND's own alpha; see class comment.</summary>
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
        /// Single place that reconciles the logical state (wanted / level) with the real HWND state.
        /// Cheap and idempotent: the 15 ms timer calls it indirectly many times per second.
        /// </summary>
        protected void SyncShellVisibility()
        {
            if (syncing) return;
            bool raiseDeactivated = false;
            syncing = true;
            try
            {
                bool visible = wanted && (level > VisibleThreshold || forceShown);

                if (visible && !IsVisible)
                {
                    base.Show();            // ShowActivated = false -> SW_SHOWNOACTIVATE
                    KeepBackdropActive();
                }
                else if (!visible && IsVisible)
                {
                    base.Hide();            // HWND (and its backdrop) stay alive
                }

                if (!visible && pseudoActive)
                {
                    pseudoActive = false;   // the logical activation ends with the window
                    raiseDeactivated = true;
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

            // What the OS would have raised when a (really) active window goes away.
            if (raiseDeactivated) OnDeactivated(EventArgs.Empty);
        }

        // ------------------------------------------------------------------ backdrop + message hook

        private void ShellWindow_SourceInitialized(object sender, EventArgs e)
        {
            wanted = true;   // the HWND exists, so somebody (e.g. StartupUri) is showing us

            // A window WPF shows on its own while logically hidden would flash an empty backdrop; park it off-screen
            // until Loaded hides it.
            if (level <= VisibleThreshold && !forceShown)
            {
                startupLeft = Left;
                Left = -32000;
            }

            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null) source.AddHook(ShellWndProc);

            ApplyBackdrop();
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        }

        private IntPtr ShellWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_MOUSEACTIVATE:
                    // The click is still delivered to us (so charms are clickable) but does not activate the
                    // window, therefore the application underneath keeps the focus.
                    if (!ActivationAllowed)
                    {
                        handled = true;
                        return (IntPtr)MA_NOACTIVATE;
                    }
                    break;

                case WM_NCACTIVATE:
                    // Mica/Acrylic are drawn "flat" when the window is not active. Keep the non-client (DWM) state
                    // active without activating the window itself.
                    if (wParam == IntPtr.Zero && Backdrop.IsActive && Backdrop.Default != BackdropType.None)
                    {
                        handled = true;
                        return DefWindowProc(hwnd, WM_NCACTIVATE, (IntPtr)1, (IntPtr)(-1));
                    }
                    break;
            }
            return IntPtr.Zero;
        }

        private void KeepBackdropActive()
        {
            if (!Backdrop.IsActive) return;
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero) PostMessage(hwnd, WM_NCACTIVATE, (IntPtr)1, (IntPtr)(-1));
        }

        /// <summary>(Re)applies the DWM backdrop, dark/light mode, corner and frame settings.</summary>
        protected void ApplyBackdrop()
        {
            Backdrop.Apply(this, Backdrop.Default, RoundedCorners);
            KeepBackdropActive();
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
