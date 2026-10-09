using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using System.Reflection.Emit;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Networking.Connectivity;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Forms;
using System.Reflection;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using static System.Resources.ResXFileRef;
using System.Net.NetworkInformation;
using System.Threading;

namespace CharmsBarPort
{
    public partial class CharmsClock : ShellWindow
    {
        // The clock floats (it does not touch a screen edge), so it gets the Windows 11 rounded corners.
        protected override bool RoundedCorners => true;

        public Microsoft.Win32.RegistryKey localKey = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64);
        public bool useTransparency = true;
        public string isDark = "";

        // Network state comes from Windows (events), see NetworkStatusMonitor.
        private NetworkStatusMonitor network;
        private NetworkState netState = new NetworkState(NetworkKind.Off, 0);
        private string appliedNetworkKey = null;
        private readonly Stopwatch signalClock = Stopwatch.StartNew();
        public CharmsClock()
        {
            var dispWidth = SystemParameters.PrimaryScreenWidth;
            var dispHeight = SystemParameters.PrimaryScreenHeight;
            Topmost = true;
            ShowInTaskbar = false;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Height = 140;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = 51;
            Top = dispHeight - 188;
            BrushConverter converter = new();
            var brush = (Brush)converter.ConvertFromString("#f0111111");
            Background = Backdrop.IsActive ? Brushes.Transparent : brush;
            System.Windows.Forms.Application.ThreadException += new ThreadExceptionEventHandler(CharmsClock.Form1_UIThreadException);
            InitializeComponent();

            network = new NetworkStatusMonitor();
            network.StateChanged += Network_StateChanged;
            // Signal strength is refreshed when the clock appears and every 10 s while it is visible; nothing runs while hidden.
            IsVisibleChanged += (s, e) => { if (IsVisible) network.RequestRefresh(); };

            _initTimer();
        }

        private void Network_StateChanged(NetworkState state)
        {
            // Raised on a thread-pool thread: hand over to the WPF UI thread.
            dispatcher.BeginInvoke((Action)(() => { netState = state; }));
        }

        protected override void OnClosed(EventArgs e)
        {
            if (network != null)
            {
                network.StateChanged -= Network_StateChanged;
                network.Dispose();
                network = null;
            }
            base.OnClosed(e);
        }

        private System.Windows.Forms.Timer t = null;
        private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;

        private void _initTimer()
        {
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = 1;
            t.Tick += OnTimedEvent;
            t.Enabled = true;
            t.Start();
        }

        private void OnTimedEvent(object sender, EventArgs e)
        {
            dispatcher.BeginInvoke((Action)(() =>
            {
                try
                {
                    RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", false);
                    if (key != null)
                    {
                        // get value 
                        string noTransparency = key.GetValue("EnableTransparency", -1, RegistryValueOptions.None).ToString(); //this is not in Windows 8.1, but used to remove transparency

                        if (noTransparency == "-1" || noTransparency == "1")
                        {
                            useTransparency = true;
                        }
                        else
                        {
                            useTransparency = false;
                        }
                    }

                    RegistryKey keys = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\ImmersiveShell\\EdgeUi", false);
                    if (keys != null)
                    {
                        // get value 
                        string noClock = keys.GetValue("DisableCharmsClock", -1, RegistryValueOptions.None).ToString(); //this is not in Windows 8.1, but used to remove the Charms Clock

                            if (noClock == "-1")
                            {
                                noClocks.Content = "0";
                            }
                            else
                            {
                                noClocks.Content = noClock;
                            }

                        } else
                    {
                        string noClock = "0";

                        if (noClock == "-1")
                        {
                            noClocks.Content = "0";
                        }
                        else
                        {
                            noClocks.Content = noClock;
                        }

                    }

                    key.Close();
                    keys.Close();
                    }
                
                catch (Exception ex)  //just for demonstration...it's always best to handle specific exceptions
                {
                    //react appropriately
                }

                if (noClocks.Content == "-1" || noClocks.Content == "0")
                {

                if (SystemParameters.HighContrast == false)
                {
                    // dark glyphs when the system theme is light and the Mica surface is light
                    isDark = (Backdrop.IsActive && Backdrop.IsLightTheme) ? "Dark" : "";
                }

                if (SystemParameters.HighContrast == true)
                {
                    System.Drawing.Color col = System.Drawing.ColorTranslator.FromHtml(SystemColors.WindowBrush.ToString());
                    if (col.R * 0.2126 + col.G * 0.7152 + col.B * 0.0722 < 255 / 2)
                    {
                        // dark color
                        isDark = "";
                    }
                    else
                    {
                        // light color
                        isDark = "Dark";
                    }
                }

                Date.Content = DateTime.Today.ToString("MMMM d");
                if (DateTime.Today.ToString("dddd") != "Sunday" && DateTime.Today.ToString("dddd") != "Monday" && DateTime.Today.ToString("dddd") != "Friday")
                {
                    Week.Content = DateTime.Today.ToString("dddd  ");
                }
                else
                {
                    Week.Content = DateTime.Today.ToString("dddd      ");
                }
                Clocks.Content = DateTime.Now.ToString("h ");
                Clocked.Content = DateTime.Now.ToString("mm");

                if (SystemParameters.HighContrast == false)
                {
                    ClockBorder.Visibility = Visibility.Hidden;
                    BrushConverter converter = new();
                    string textColor = "#ffffff";
                    if (Backdrop.IsActive)
                    {
                        // Real Mica from DWM: the WPF surface stays transparent.
                        this.Background = Brushes.Transparent;
                        if (Backdrop.IsLightTheme) textColor = "#1b1b1b";
                    }
                    else if (useTransparency == false)
                    {
                        this.Background = (Brush)converter.ConvertFromString("#111111");
                    }
                    else
                    {
                        this.Background = (Brush)converter.ConvertFromString("#f0111111");
                    }
                    ClockLines.Foreground = (Brush)converter.ConvertFromString(textColor);
                    Clocks.Foreground = (Brush)converter.ConvertFromString(textColor);
                    Week.Foreground = (Brush)converter.ConvertFromString(textColor);
                    Date.Foreground = (Brush)converter.ConvertFromString(textColor);
                    Clocked.Foreground = (Brush)converter.ConvertFromString(textColor);
                }

                if (SystemParameters.HighContrast == true)
                {
                    this.Background = SystemColors.WindowBrush;
                    ClockBorder.Visibility = Visibility.Visible;
                    ClockLines.Foreground = SystemColors.WindowTextBrush;
                    Clocks.Foreground = SystemColors.WindowTextBrush;
                    Week.Foreground = SystemColors.WindowTextBrush;
                    Date.Foreground = SystemColors.WindowTextBrush;
                    Clocked.Foreground = SystemColors.WindowTextBrush;
                }

                if (this.IsVisible && network != null && signalClock.ElapsedMilliseconds > 10000)
                {
                    signalClock.Restart();
                    network.RequestRefresh();
                }
                CheckBatteryStatus();
                ClockBorder.BorderBrush = SystemColors.WindowTextBrush;
                ClockBorder.Background = SystemColors.WindowBrush;

                UpdateNetworkIcon();

                if (Clocks.Content.ToString().Length < 3 && Clocks.Content.ToString().StartsWith("1 ") == false && Clocks.Content.ToString().StartsWith("10") == false && Clocks.Content.ToString().StartsWith("11") == false && Clocks.Content.ToString().StartsWith("12") == false || Clocks.Content.ToString().Length == 2 && Clocks.Content.ToString().StartsWith("1 ") == false && Clocks.Content.ToString().StartsWith("10") == false && Clocks.Content.ToString().StartsWith("11") == false && Clocks.Content.ToString().StartsWith("12") == false)
                {
                    Clocks.Margin = new Thickness(94, 3, 0, -106);
                    ClockLines.Margin = new Thickness(138, -24.99, -190, -98);
                    Clocked.Margin = new Thickness(157, -17, -190, -198);
                    Week.Margin = new Thickness(267, 2, 0, -18);
                    Date.Margin = new Thickness(269, 3, 0, -24);
                }

                if (Clocks.Content.ToString().StartsWith("1 ") == true)
                {
                    Clocks.Margin = new Thickness(95, 3, 0, -106);
                    ClockLines.Margin = new Thickness(125, -24.99, -190, -98);
                    Clocked.Margin = new Thickness(144, -17, -190, -198);
                    Week.Margin = new Thickness(255, 2, 0, -18);
                    Date.Margin = new Thickness(255, 3, 0, -24);
                }

                if (Clocks.Content.ToString().StartsWith("10") == true)
                {
                    Clocks.Margin = new Thickness(95, 3, 0, -106);
                    ClockLines.Margin = new Thickness(169, -24.99, -190, -98);
                    Clocked.Margin = new Thickness(188, -17, -190, -198);
                    Week.Margin = new Thickness(298, 2, 0, -18);
                    Date.Margin = new Thickness(300, 4, 0, -24);
                }

                if (Clocks.Content.ToString().StartsWith("11") == true)
                {
                    Clocks.Margin = new Thickness(95, 3, 0, -106);
                    ClockLines.Margin = new Thickness(156, -24.99, -190, -98);
                    Clocked.Margin = new Thickness(174.5, -17, -190, -198);
                    Week.Margin = new Thickness(284, 2, 0, -18);
                    Date.Margin = new Thickness(284, 4, 0, -24);
                }

                if (Clocks.Content.ToString().StartsWith("12") == true)
                {
                    Clocks.Margin = new Thickness(95, 3, 0, -106);
                    ClockLines.Margin = new Thickness(169, -24.99, -190, -98);
                    Clocked.Margin = new Thickness(188, -17, -190, -198);
                    Week.Margin = new Thickness(298, 2, 0, -18);
                    Date.Margin = new Thickness(300, 4, 0, -24);
                }

                if (Clocks.Content.ToString().Length == 3 && Clocks.Content.ToString().StartsWith("10") == false && Clocks.Content.ToString().StartsWith("12") == false)
                {
                    Clocks.Margin = new Thickness(94, 3, 0, -106);
                    ClockLines.Margin = new Thickness(157, -24.99, -190, -98);
                    Clocked.Margin = new Thickness(175, -17, -190, -198);
                    Week.Margin = new Thickness(287, 2, 0, -18);
                    Date.Margin = new Thickness(287, 4, 0, -24);
                }

                if (Date.Content.ToString().Length > 6 || Date.Content.ToString().Length < 7 && Week.Content.ToString().Length > 9)
                {
                    if (Clocks.Content.ToString().Length == 2 && Clocks.Content.ToString().StartsWith("1") == false)
                    {
                        CharmClock.Margin = new Thickness(0, 0, 10, 0);
                        AutoResizer.Width = 391 + Date.Content.ToString().Length + 25;
                    }

                    if (Clocks.Content.ToString().Length == 2 && Clocks.Content.ToString().StartsWith("1") == true)
                    {
                        CharmClock.Margin = new Thickness(0, 0, 10, 0);
                        AutoResizer.Width = 391 + Date.Content.ToString().Length;
                    }

                    if (Clocks.Content.ToString().Length == 3 && Clocks.Content.ToString().StartsWith("10") == true)
                    {
                        CharmClock.Margin = new Thickness(0, 0, 10, 0);
                        AutoResizer.Width = 391 + Date.Content.ToString().Length + 63;
                    }

                    if (Clocks.Content.ToString().Length == 3 && Clocks.Content.ToString().StartsWith("12") == true)
                    {
                        CharmClock.Margin = new Thickness(0, 0, 10, 0);
                        AutoResizer.Width = 391 + Date.Content.ToString().Length + 63;
                    }

                    if (Clocks.Content.ToString().Length == 3 && Clocks.Content.ToString().StartsWith("11") == true)
                    {
                        CharmClock.Margin = new Thickness(0, 0, 10, 0);
                        AutoResizer.Width = 391 + Date.Content.ToString().Length + 63;
                    }

                    if (Clocks.Content.ToString().Length == 3 && Clocks.Content.ToString().StartsWith("10") == false && Clocks.Content.ToString().StartsWith("11") == false && Clocks.Content.ToString().StartsWith("12") == false && Clocks.Content.ToString().StartsWith("1") == false)
                    {
                        CharmClock.Margin = new Thickness(0, 0, 10, 0);
                        AutoResizer.Width = 391 + Date.Content.ToString().Length + 25;
                    }
                }

                ClockBorder.Width = this.Width;
                    }
            }));
        }

        // Handle the UI exceptions by showing a dialog box, and asking the user whether
        // or not they wish to abort execution.
        private static void Form1_UIThreadException(object sender, ThreadExceptionEventArgs t)
        {
            System.Windows.Forms.Application.Restart();
        }

        // ------------------------------------------------------------------------------------------------
        // Asset lookup. Only assets that really exist in the project are ever used; nothing is generated.
        // ------------------------------------------------------------------------------------------------

        private readonly Dictionary<string, BitmapImage> assetCache = new Dictionary<string, BitmapImage>();

        private BitmapImage LoadAsset(string file)
        {
            BitmapImage cached;
            if (assetCache.TryGetValue(file, out cached)) return cached;

            BitmapImage image = null;
            try
            {
                var uri = new Uri("pack://application:,,,/Assets/Images/" + file + ".png", UriKind.Absolute);
                var resource = System.Windows.Application.GetResourceStream(uri);
                if (resource != null)
                {
                    using (resource.Stream)
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;   // decode now, then the stream can be closed
                        bmp.StreamSource = resource.Stream;
                        bmp.EndInit();
                        bmp.Freeze();
                        image = bmp;
                    }
                }
            }
            catch (Exception)
            {
                image = null;   // missing / unreadable asset: handled by the caller, never fatal
            }

            assetCache[file] = image;
            return image;
        }

        /// <summary>
        /// Loads "name" + the current theme suffix ("Dark" or ""); if that variant does not exist, the other
        /// variant of the same icon. Returns null when the icon does not exist at all.
        /// </summary>
        private BitmapImage LoadThemed(string name)
        {
            string preferred = name + isDark;
            string other = name + (isDark == "" ? "Dark" : "");
            return LoadAsset(preferred) ?? LoadAsset(other);
        }

        private BitmapImage LoadFirst(params string[] names)
        {
            foreach (string n in names)
            {
                var image = LoadThemed(n);
                if (image != null) return image;
            }
            return null;
        }

        // ------------------------------------------------------------------------------------------------
        // Network icon
        // ------------------------------------------------------------------------------------------------

        private static string NetworkAssetName(NetworkState state)
        {
            switch (state.Kind)
            {
                case NetworkKind.WiFi: return state.Level >= 1 && state.Level <= 4 ? "WiFi" + state.Level : null;
                case NetworkKind.Cell: return state.Level >= 1 && state.Level <= 5 ? "Cell" + state.Level : null;
                case NetworkKind.WiFiError: return "WiFiError";
                case NetworkKind.CellError: return "CellError";
                case NetworkKind.Other: return "Other";
                case NetworkKind.Error: return "NetworkError";
                default: return "NetworkOff";
            }
        }

        private void UpdateNetworkIcon()
        {
            string name = NetworkAssetName(netState);
            string key = (name ?? "-") + "|" + isDark;
            if (key == appliedNetworkKey) return;   // nothing changed: no work on the 1 ms tick
            appliedNetworkKey = key;

            // The old per-state images are not used any more; one image shows the selected icon.
            NoDrivers.Visibility = Visibility.Hidden;
            NoInternet.Visibility = Visibility.Hidden;
            NoInternetFound.Visibility = Visibility.Hidden;
            Ethernet.Visibility = Visibility.Hidden;
            WeakInternet.Visibility = Visibility.Hidden;
            Airplane.Visibility = Visibility.Hidden;

            // Unknown level (e.g. cellular signal not exposed by Windows) or a missing asset: show nothing
            // rather than a wrong or invented icon.
            BitmapImage icon = name == null ? null : LoadThemed(name);
            if (icon == null)
            {
                HasInternet.Visibility = Visibility.Hidden;
                return;
            }

            HasInternet.Source = icon;
            HasInternet.Visibility = Visibility.Visible;
        }

        // ------------------------------------------------------------------------------------------------
        // Battery icon
        // ------------------------------------------------------------------------------------------------

        private readonly Stopwatch batteryClock = Stopwatch.StartNew();
        private long lastBatteryTick = -100000;
        private string lastBatteryTheme = null;

        private static int BatteryLevel(int percent)
        {
            // Supported levels: 1, 5, 10, 20 ... 90 (and Full).
            if (percent >= 90) return 90;
            if (percent >= 10) return (percent / 10) * 10;
            if (percent >= 5) return 5;
            return 1;
        }

        private void CheckBatteryStatus()
        {
            // The power state changes slowly; the 1 ms tick must not hit it every time.
            if (batteryClock.ElapsedMilliseconds - lastBatteryTick < 1000 && lastBatteryTheme == isDark) return;
            lastBatteryTick = batteryClock.ElapsedMilliseconds;
            lastBatteryTheme = isDark;

            var power = SystemInformation.PowerStatus;
            BatteryChargeStatus status = power.BatteryChargeStatus;

            // The separate plug overlay is gone: charging has its own icons.
            IsCharging.Visibility = Visibility.Hidden;

            BitmapImage icon;
            if ((status & BatteryChargeStatus.NoSystemBattery) != 0)   // "Unknown" (255) contains this bit too
            {
                icon = LoadFirst("BatteryMissing");
            }
            else
            {
                int percent = (int)Math.Round(power.BatteryLifePercent * 100.0);
                bool charging = power.PowerLineStatus == PowerLineStatus.Online || (status & BatteryChargeStatus.Charging) != 0;
                bool full = percent >= 96;
                int level = BatteryLevel(percent);

                if (charging)
                {
                    icon = full
                        ? LoadFirst("BatteryChargingFull", "BatteryFullCharging", "BatteryFull")
                        : LoadFirst("BatteryCharging" + level, "Battery" + level);
                }
                else
                {
                    icon = full ? LoadFirst("BatteryFull") : LoadFirst("Battery" + level);
                }
            }

            if (icon == null)
            {
                BatteryLife.Visibility = Visibility.Hidden;   // missing asset: no icon rather than an unrelated one
                return;
            }

            BatteryLife.Source = icon;
            BatteryLife.Visibility = Visibility.Visible;
        }
    }

}