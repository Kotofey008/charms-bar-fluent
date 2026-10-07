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
    public partial class CharmsMenu : ShellWindow
    {
        ShellWindow CharmsClock = new CharmsClock();
        public Microsoft.Win32.RegistryKey localKey = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64);
        public bool charmsMenuOpen = false;
        BrushConverter converter = new();
        public CharmsMenu()
        {
            var dispWidth = SystemParameters.PrimaryScreenWidth;
            var dispHeight = SystemParameters.PrimaryScreenHeight;
            Topmost = true;
            ShowInTaskbar = false;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            var brush = (Brush)converter.ConvertFromString("#00111111");
            Background = brush;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = 0;
            Top = dispHeight - 190;   // panel now starts at the window top (old 10px transparent margin removed)
            System.Windows.Forms.Application.ThreadException += new ThreadExceptionEventHandler(CharmsMenu.Form1_UIThreadException);
            InitializeComponent();
            _initTimer();
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
                var dispWidth = SystemParameters.PrimaryScreenWidth;
                var dispHeight = SystemParameters.PrimaryScreenHeight;
                try
                {
                    RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\ImmersiveShell\\EdgeUi", false);
                    if (key != null)
                    {
                        // get value 
                        string charmMenuUse = key.GetValue("EnableCharmsMenu", -1, RegistryValueOptions.None).ToString(); //this is not in Windows 8.1, but used to remove the Charms Clock

                        if (charmMenuUse == "-1")
                        {
                            useMenu.Content = "0";
                        }
                        else
                        {
                            useMenu.Content = charmMenuUse;
                        }

                    }
                    key.Close();
                }

                catch (Exception ex)  //just for demonstration...it's always best to handle specific exceptions
                {
                    //react appropriately
                }

                if (charmsMenuOpen == true)
                {
                    CharmsClock.Left = dispWidth - 527;
                }

                ApplyTheme();

            }));
        }

        private int menuTheme = -1; // 0 legacy dark, 1 Mica dark, 2 Mica light
        private static readonly Brush darkText = Freeze("#D4D4D4");
        private static readonly Brush lightText = Freeze("#505050");
        private static readonly Brush legacyText = Freeze("#A0A0A0");

        private static Brush Freeze(string hex)
        {
            var b = (Brush)new BrushConverter().ConvertFromString(hex);
            b.Freeze();
            return b;
        }

        /// <summary>Text/icon colours follow the system light/dark theme; the backdrop itself is drawn by DWM.</summary>
        private void ApplyTheme()
        {
            int state = !Backdrop.IsActive ? 0 : (Backdrop.IsLightTheme ? 2 : 1);
            if (state == menuTheme) return;
            menuTheme = state;

            Brush text = state == 2 ? lightText : (state == 1 ? darkText : legacyText);
            SettingsText.Foreground = text;
            DevicesText.Foreground = text;
            ShareText.Foreground = text;
            SearchText.Foreground = text;
            WinText.Foreground = text;

            string d = state == 2 ? "Dark" : "";
            SettingsCharm.Source = new BitmapImage(new Uri(@"/Assets/Images/Settings" + d + ".png", UriKind.Relative));
            DevicesCharm.Source = new BitmapImage(new Uri(@"/Assets/Images/Devices" + d + ".png", UriKind.Relative));
            ShareCharm.Source = new BitmapImage(new Uri(@"/Assets/Images/Share" + d + ".png", UriKind.Relative));
            SearchCharm.Source = new BitmapImage(new Uri(@"/Assets/Images/Search" + d + ".png", UriKind.Relative));
        }

        // Handle the UI exceptions by showing a dialog box, and asking the user whether
        // or not they wish to abort execution.
        private static void Form1_UIThreadException(object sender, ThreadExceptionEventArgs t)
        {
            System.Windows.Forms.Application.Restart();
        }
    }
}