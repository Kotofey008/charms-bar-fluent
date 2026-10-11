using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace CharmsBarMica
{
    public enum StartLogoKind { Windows7, Windows8, Windows11 }

    /// <summary>
    /// Chooses which existing Windows logo the Start charm shows, once, before any window is created:
    ///   -7 or --Win7 (any case) on the command line -> Windows 7 logo
    ///   otherwise Windows 11 (build 22000+)         -> Windows 11 logo
    ///   otherwise (Windows 10, or anything older)   -> Windows 8 logo (the original)
    /// The switch changes the logo only; nothing else in the application looks at it.
    /// </summary>
    public static class StartLogo
    {
        private static StartLogoKind? kind;

        public static StartLogoKind Kind
        {
            get
            {
                if (!kind.HasValue) kind = Detect();
                return kind.Value;
            }
        }

        /// <summary>Asset file prefix: "Windows7", "Windows8" or "Windows11" (e.g. Prefix + "Inactive" + ".png").</summary>
        public static string Prefix
        {
            get
            {
                switch (Kind)
                {
                    case StartLogoKind.Windows7: return "Windows7";
                    case StartLogoKind.Windows11: return "Windows11";
                    default: return "Windows8";
                }
            }
        }

        private static StartLogoKind Detect()
        {
            if (HasWin7Switch(Environment.GetCommandLineArgs())) return StartLogoKind.Windows7;
            return GetWindowsBuild() >= 22000 ? StartLogoKind.Windows11 : StartLogoKind.Windows8;
        }

        /// <summary>True when any argument is exactly "-7" or "--Win7" (case-insensitive). Other arguments are ignored.</summary>
        private static bool HasWin7Switch(string[] args)
        {
            // args[0] is the executable path
            for (int i = 1; i < args.Length; i++)
            {
                string a = args[i];
                if (string.Equals(a, "-7", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "--Win7", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // ---- Windows version -------------------------------------------------------------------------------
        // Windows 10 and 11 are both "10.0"; only the build number tells them apart (11 = 22000 and newer).
        // Environment.OSVersion / GetVersionEx can be altered by compatibility settings, so the build is read from
        // the registry (not affected by compatibility shims) and from RtlGetVersion, and the higher one is used.

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private struct OSVERSIONINFOEX
        {
            public int dwOSVersionInfoSize;
            public int dwMajorVersion;
            public int dwMinorVersion;
            public int dwBuildNumber;
            public int dwPlatformId;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 128)] public string szCSDVersion;
            public ushort wServicePackMajor;
            public ushort wServicePackMinor;
            public ushort wSuiteMask;
            public byte wProductType;
            public byte wReserved;
        }

        [System.Runtime.InteropServices.DllImport("ntdll.dll")]
        private static extern int RtlGetVersion(ref OSVERSIONINFOEX info);

        private static int GetWindowsBuild()
        {
            int build = 0;

            try
            {
                var info = new OSVERSIONINFOEX();
                info.dwOSVersionInfoSize = System.Runtime.InteropServices.Marshal.SizeOf<OSVERSIONINFOEX>();
                if (RtlGetVersion(ref info) == 0) build = Math.Max(build, info.dwBuildNumber);
            }
            catch { }

            try
            {
                using (var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                                            .OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", false))
                {
                    int b;
                    if (key != null && int.TryParse(Convert.ToString(key.GetValue("CurrentBuildNumber")), out b)) build = Math.Max(build, b);
                }
            }
            catch { }

            if (build == 0) build = Environment.OSVersion.Version.Build;
            return build;
        }

        // ---- Logo shapes -----------------------------------------------------------------------------------
        // The accent-coloured logo is clipped to the shape of the logo's hole in the existing plate image
        // (Windows7.png, Windows8.png, Windows11Hover.png - Windows 11 has no base plate, its Hover/Down plates share
        // the same hole), converted to vector outlines once. Coordinates: 140x140 plate scaled to the host.

        private static string BarShape(StartLogoKind k)
        {
            switch (k)
            {
                case StartLogoKind.Windows7: return W7BAR;
                case StartLogoKind.Windows11: return W11BAR;
                default: return W8BAR;
            }
        }

        private static string MenuShape(StartLogoKind k)
        {
            switch (k)
            {
                case StartLogoKind.Windows7: return W7MENU;
                case StartLogoKind.Windows11: return W11MENU;
                default: return W8MENU;
            }
        }

        /// <summary>Applies the selected logo shape to the accent host (bar: 80.31 x 82, menu: 45 x 45).</summary>
        public static void ApplyTo(UIElement accentHost, bool bar)
        {
            string data = bar ? BarShape(Kind) : MenuShape(Kind);
            Geometry geometry = Geometry.Parse(data);
            geometry.Freeze();
            accentHost.Clip = geometry;
        }

        private const string W7BAR = "M 38.72,39.26 L 38.15,38.82 L 35.85,37.64 L 34.13,37.04 L 30.69,36.38 L 27.25,36.43 L 24.95,36.84 L 23.81,37.06 L 22.09,37.59 L 20.36,38.30 L 19.96,37.78 L 23.06,26.06 L 23.32,25.48 L 23.68,23.72 L 23.92,23.14 L 24.34,21.38 L 24.95,20.20 L 28.97,18.88 L 31.26,18.37 L 32.98,18.14 L 35.28,18.11 L 37.00,18.33 L 39.29,18.97 L 41.59,19.98 L 42.74,20.58 L 43.91,21.38 L 44.09,21.96 L 43.80,22.55 L 39.40,38.36 L 39.16,38.95 L 38.72,39.26 Z M 54.78,44.36 L 51.34,44.68 L 48.47,44.42 L 46.75,43.94 L 45.03,43.28 L 42.74,42.10 L 41.66,41.29 L 41.66,40.71 L 43.12,35.44 L 43.38,34.85 L 43.78,33.09 L 44.04,32.51 L 44.93,28.99 L 45.19,28.41 L 46.52,23.72 L 46.75,23.49 L 49.62,25.08 L 51.34,25.68 L 54.78,26.36 L 58.80,26.31 L 62.24,25.59 L 63.96,25.07 L 65.11,24.54 L 65.68,24.69 L 65.78,24.89 L 65.52,25.48 L 65.30,26.65 L 64.89,27.82 L 64.67,28.99 L 62.30,37.78 L 62.06,38.36 L 61.83,39.54 L 61.42,40.71 L 61.19,41.88 L 60.83,42.46 L 60.52,42.69 L 57.08,43.79 L 54.78,44.36 Z M 33.56,60.09 L 32.98,60.35 L 32.41,59.89 L 30.12,58.75 L 28.40,58.16 L 24.95,57.51 L 22.09,57.51 L 19.22,57.99 L 16.35,58.76 L 14.63,59.51 L 14.54,59.45 L 14.29,58.86 L 14.60,57.69 L 16.78,49.49 L 17.04,48.91 L 17.28,47.74 L 17.68,46.56 L 18.40,43.64 L 18.90,41.88 L 19.22,41.43 L 19.79,41.11 L 23.81,39.90 L 27.25,39.27 L 29.54,39.23 L 30.69,39.33 L 33.56,40.08 L 35.85,41.07 L 38.20,42.46 L 38.43,43.05 L 38.01,44.22 L 37.76,45.39 L 37.35,46.56 L 37.10,47.74 L 36.86,48.32 L 33.56,60.09 Z M 49.05,65.48 L 47.33,65.71 L 45.03,65.84 L 42.16,65.46 L 40.44,64.91 L 38.72,64.19 L 36.56,62.96 L 35.87,62.38 L 35.87,61.79 L 36.83,58.28 L 37.08,57.69 L 37.33,56.52 L 37.75,55.35 L 40.31,45.98 L 40.79,44.81 L 41.02,44.63 L 42.74,45.70 L 45.03,46.67 L 46.84,47.15 L 49.05,47.50 L 52.49,47.49 L 54.78,47.17 L 56.50,46.73 L 59.37,45.64 L 59.94,45.98 L 56.68,58.28 L 56.42,58.86 L 56.03,60.62 L 55.76,61.21 L 55.38,62.96 L 55.07,63.55 L 54.78,63.79 L 51.34,64.91 L 49.62,65.26 L 49.05,65.48 Z";
        private const string W7MENU = "M 21.70,21.54 L 21.38,21.30 L 20.09,20.65 L 19.12,20.33 L 17.20,19.96 L 15.27,19.99 L 13.98,20.22 L 13.34,20.34 L 12.38,20.63 L 11.41,21.02 L 11.18,20.73 L 12.92,14.30 L 13.07,13.98 L 13.27,13.02 L 13.40,12.70 L 13.64,11.73 L 13.98,11.08 L 16.23,10.36 L 17.52,10.08 L 18.48,9.96 L 19.77,9.94 L 20.73,10.06 L 22.02,10.41 L 23.30,10.96 L 23.95,11.29 L 24.60,11.73 L 24.70,12.05 L 24.54,12.38 L 22.08,21.05 L 21.94,21.38 L 21.70,21.54 Z M 30.70,24.35 L 28.77,24.52 L 27.16,24.38 L 26.20,24.11 L 25.23,23.75 L 23.95,23.11 L 23.34,22.66 L 23.34,22.34 L 24.16,19.45 L 24.30,19.12 L 24.53,18.16 L 24.68,17.84 L 25.18,15.91 L 25.32,15.59 L 26.07,13.02 L 26.20,12.89 L 27.80,13.76 L 28.77,14.09 L 30.70,14.47 L 32.95,14.44 L 34.88,14.04 L 35.84,13.76 L 36.48,13.46 L 36.80,13.55 L 36.86,13.66 L 36.71,13.98 L 36.59,14.63 L 36.36,15.27 L 36.23,15.91 L 34.91,20.73 L 34.77,21.05 L 34.65,21.70 L 34.42,22.34 L 34.29,22.98 L 34.08,23.30 L 33.91,23.43 L 31.98,24.03 L 30.70,24.35 Z M 18.80,32.98 L 18.48,33.12 L 18.16,32.87 L 16.88,32.24 L 15.91,31.92 L 13.98,31.56 L 12.38,31.56 L 10.77,31.82 L 9.16,32.24 L 8.20,32.66 L 8.15,32.62 L 8.01,32.30 L 8.18,31.66 L 9.40,27.16 L 9.55,26.84 L 9.68,26.20 L 9.91,25.55 L 10.31,23.95 L 10.59,22.98 L 10.77,22.74 L 11.09,22.56 L 13.34,21.90 L 15.27,21.55 L 16.55,21.53 L 17.20,21.58 L 18.80,22.00 L 20.09,22.54 L 21.41,23.30 L 21.53,23.62 L 21.30,24.27 L 21.16,24.91 L 20.93,25.55 L 20.79,26.20 L 20.65,26.52 L 18.80,32.98 Z M 27.48,35.93 L 26.52,36.06 L 25.23,36.13 L 23.62,35.93 L 22.66,35.62 L 21.70,35.23 L 20.48,34.55 L 20.10,34.23 L 20.10,33.91 L 20.63,31.98 L 20.78,31.66 L 20.92,31.02 L 21.15,30.38 L 22.59,25.23 L 22.86,24.59 L 22.98,24.49 L 23.95,25.08 L 25.23,25.61 L 26.24,25.88 L 27.48,26.07 L 29.41,26.06 L 30.70,25.89 L 31.66,25.64 L 33.27,25.05 L 33.59,25.23 L 31.76,31.98 L 31.61,32.30 L 31.39,33.27 L 31.25,33.59 L 31.03,34.55 L 30.86,34.88 L 30.70,35.00 L 28.77,35.62 L 27.80,35.81 L 27.48,35.93 Z";
        private const string W8BAR = "M 38.00,22.55 L 60.66,19.48 L 60.52,38.95 L 38.29,38.95 Z M 19.36,24.75 L 36.14,22.99 L 35.85,38.95 L 18.93,38.80 Z M 19.36,40.71 L 35.71,40.71 L 36.14,56.67 L 18.93,54.62 Z M 38.43,40.71 L 60.38,40.71 L 60.66,60.18 L 38.29,57.40 Z";
        private const string W8MENU = "M 21.29,12.38 L 33.99,10.69 L 33.91,21.38 L 21.46,21.38 Z M 10.85,13.58 L 20.25,12.62 L 20.09,21.38 L 10.61,21.29 Z M 10.85,22.34 L 20.01,22.34 L 20.25,31.10 L 10.61,29.97 Z M 21.54,22.34 L 33.83,22.34 L 33.99,33.03 L 21.46,31.50 Z";
        private const string W11BAR = "M 38.72,39.01 L 18.64,39.08 L 18.07,39.00 L 17.50,38.72 L 17.31,38.36 L 17.34,17.86 L 17.50,17.71 L 18.07,17.67 L 38.72,17.73 L 38.85,17.86 L 38.85,38.36 L 38.72,39.01 Z M 61.67,39.08 L 41.59,39.08 L 40.96,38.95 L 40.88,17.86 L 41.02,17.73 L 61.67,17.67 L 62.24,17.71 L 62.40,17.86 L 62.43,38.36 L 62.24,38.80 L 61.67,39.08 Z M 38.15,63.16 L 17.50,63.12 L 17.34,62.96 L 17.31,62.38 L 17.31,50.66 L 17.40,48.91 L 17.31,42.46 L 17.50,42.11 L 18.07,41.83 L 18.64,41.75 L 38.72,41.82 L 38.85,42.46 L 38.85,62.38 L 38.58,62.96 L 38.15,63.16 Z M 62.24,63.12 L 41.59,63.16 L 41.16,62.96 L 40.89,62.38 L 40.96,41.88 L 41.59,41.75 L 61.67,41.75 L 62.24,42.03 L 62.43,42.46 L 62.43,62.38 L 62.40,62.96 L 62.24,63.12 Z";
        private const string W11MENU = "M 21.70,21.41 L 10.45,21.45 L 10.12,21.40 L 9.80,21.25 L 9.70,21.05 L 9.72,9.80 L 9.80,9.72 L 10.12,9.70 L 21.70,9.73 L 21.77,9.80 L 21.77,21.05 L 21.70,21.41 Z M 34.55,21.45 L 23.30,21.45 L 22.95,21.38 L 22.91,9.80 L 22.98,9.73 L 34.55,9.70 L 34.88,9.72 L 34.96,9.80 L 34.98,21.05 L 34.88,21.29 L 34.55,21.45 Z M 21.38,34.66 L 9.80,34.64 L 9.72,34.55 L 9.70,34.23 L 9.70,27.80 L 9.75,26.84 L 9.70,23.30 L 9.80,23.11 L 10.12,22.95 L 10.45,22.91 L 21.70,22.95 L 21.77,23.30 L 21.77,34.23 L 21.62,34.55 L 21.38,34.66 Z M 34.88,34.64 L 23.30,34.66 L 23.06,34.55 L 22.91,34.23 L 22.95,22.98 L 23.30,22.91 L 34.55,22.91 L 34.88,23.06 L 34.98,23.30 L 34.98,34.23 L 34.96,34.55 L 34.88,34.64 Z";
    }
}
