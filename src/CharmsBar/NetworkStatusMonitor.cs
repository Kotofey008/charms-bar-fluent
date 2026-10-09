using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.Connectivity;

namespace CharmsBarPort
{
    /// <summary>What the clock's network icon has to show.</summary>
    public enum NetworkKind
    {
        Off,         // no adapter is connected                          -> NetworkOff
        Error,       // adapters connected, none has internet            -> NetworkError
        WiFiError,   // Wi-Fi is the only connected type, no internet    -> WiFiError
        CellError,   // cellular is the only connected type, no internet -> CellError
        Other,       // internet through Ethernet / Bluetooth / virtual  -> Other
        WiFi,        // internet through Wi-Fi, Level 1..4 (0 = unknown) -> WiFi<Level>
        Cell         // internet through cellular, Level 1..5 (0 = unknown) -> Cell<Level>
    }

    public sealed record NetworkState(NetworkKind Kind, int Level);

    /// <summary>
    /// Event-driven network state, built on Windows.Networking.Connectivity (connection profiles, connectivity
    /// level, NetworkStatusChanged) and the Native Wi-Fi API (wlanapi.dll) for the Wi-Fi signal quality.
    /// No processes are started and nothing is probed on the network: only the connectivity level that Windows
    /// itself reports is used.
    /// </summary>
    public sealed class NetworkStatusMonitor : IDisposable
    {
        /// <summary>Raised on a thread-pool thread whenever the evaluated state changes. Marshal to the UI thread.</summary>
        public event Action<NetworkState> StateChanged;

        private readonly NetworkStatusChangedEventHandler statusHandler;
        private readonly object gate = new object();
        private NetworkState current = new NetworkState(NetworkKind.Off, 0);
        private int refreshQueued;
        private volatile bool disposed;

        public NetworkStatusMonitor()
        {
            statusHandler = OnNetworkStatusChanged;
            NetworkInformation.NetworkStatusChanged += statusHandler;
            RequestRefresh();
        }

        public NetworkState Current
        {
            get { lock (gate) { return current; } }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { NetworkInformation.NetworkStatusChanged -= statusHandler; } catch { }
        }

        private void OnNetworkStatusChanged(object sender)
        {
            RequestRefresh();
        }

        /// <summary>
        /// Re-evaluates the state off the calling thread. Bursts of events (an adapter coming up raises several)
        /// are coalesced into a single evaluation.
        /// </summary>
        public void RequestRefresh()
        {
            if (disposed) return;
            if (Interlocked.Exchange(ref refreshQueued, 1) != 0) return;

            Task.Run(async () =>
            {
                await Task.Delay(250).ConfigureAwait(false);
                Interlocked.Exchange(ref refreshQueued, 0);
                if (disposed) return;

                NetworkState next;
                try { next = Evaluate(); }
                catch { return; }          // keep the last known state if Windows cannot answer right now

                bool changed;
                lock (gate)
                {
                    changed = !next.Equals(current);
                    current = next;
                }
                if (changed) StateChanged?.Invoke(next);
            });
        }

        // ------------------------------------------------------------------------------------------------
        // Decision logic
        // ------------------------------------------------------------------------------------------------

        private enum Type { WiFi, Cell, Other }

        private sealed class Connection
        {
            public ConnectionProfile Profile;
            public Type Type;
            public bool Internet;
        }

        private static NetworkState Evaluate()
        {
            var connected = new List<Connection>();

            foreach (ConnectionProfile p in NetworkInformation.GetConnectionProfiles())
            {
                NetworkConnectivityLevel level;
                try { level = p.GetNetworkConnectivityLevel(); }
                catch { continue; }

                if (level == NetworkConnectivityLevel.None) continue;   // not connected

                Type type;
                if (p.IsWlanConnectionProfile) type = Type.WiFi;
                else if (p.IsWwanConnectionProfile) type = Type.Cell;
                else
                {
                    // IANA ifType 24 = software loopback, which is not a network connection.
                    uint iana = 0;
                    try { iana = p.NetworkAdapter != null ? p.NetworkAdapter.IanaInterfaceType : 0; } catch { }
                    if (iana == 24) continue;
                    type = Type.Other;
                }

                connected.Add(new Connection
                {
                    Profile = p,
                    Type = type,
                    // Only a full InternetAccess level counts. LocalAccess and ConstrainedInternetAccess (captive
                    // portal / limited) are "connected, but no internet".
                    Internet = level == NetworkConnectivityLevel.InternetAccess
                });
            }

            // 1. No connected adapter at all (this is also what Airplane Mode leaves behind).
            if (connected.Count == 0) return new NetworkState(NetworkKind.Off, 0);

            // 2. At least one connection provides internet: show the one Windows routes internet through.
            var withInternet = connected.FindAll(c => c.Internet);
            if (withInternet.Count > 0)
            {
                Connection chosen = null;

                ConnectionProfile preferred = null;
                try { preferred = NetworkInformation.GetInternetConnectionProfile(); } catch { }

                if (preferred != null)
                {
                    foreach (var c in withInternet)
                    {
                        if (SameConnection(c.Profile, preferred)) { chosen = c; break; }
                    }
                }
                if (chosen == null) chosen = withInternet[0];

                switch (chosen.Type)
                {
                    case Type.WiFi: return new NetworkState(NetworkKind.WiFi, WifiLevel(chosen.Profile));
                    case Type.Cell: return new NetworkState(NetworkKind.Cell, CellLevel(chosen.Profile));
                    default: return new NetworkState(NetworkKind.Other, 0);
                }
            }

            // 3. Connected, but nothing has internet.
            bool allWifi = connected.TrueForAll(c => c.Type == Type.WiFi);
            bool allCell = connected.TrueForAll(c => c.Type == Type.Cell);
            if (allWifi) return new NetworkState(NetworkKind.WiFiError, 0);
            if (allCell) return new NetworkState(NetworkKind.CellError, 0);
            return new NetworkState(NetworkKind.Error, 0);
        }

        private static bool SameConnection(ConnectionProfile a, ConnectionProfile b)
        {
            try
            {
                if (a.NetworkAdapter != null && b.NetworkAdapter != null)
                    return a.NetworkAdapter.NetworkAdapterId == b.NetworkAdapter.NetworkAdapterId;
            }
            catch { }
            return false;
        }

        // ------------------------------------------------------------------------------------------------
        // Signal strength
        // ------------------------------------------------------------------------------------------------

        /// <summary>Wi-Fi level 1 (weakest) .. 4 (strongest); 0 when Windows gives no value.</summary>
        private static int WifiLevel(ConnectionProfile profile)
        {
            int? quality = QueryWlanSignalQuality(profile);   // 0..100, from the Native Wi-Fi API
            if (quality.HasValue)
            {
                int q = quality.Value;
                if (q <= 25) return 1;
                if (q <= 50) return 2;
                if (q <= 75) return 3;
                return 4;
            }

            // wlanapi unavailable (e.g. WLAN service stopped): fall back to the signal bars Windows reports
            // for the profile (0..5). If that is missing too the level stays unknown - no value is invented.
            try
            {
                byte? bars = profile.GetSignalBars();
                if (bars.HasValue) return Math.Max(1, (int)Math.Round(Math.Min((int)bars.Value, 5) * 4 / 5.0));
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Cellular level 1..5 from the profile's signal bars. Windows does not always expose bars to a desktop
        /// application; in that case the level is 0 (unknown) and the caller shows no level icon.
        /// </summary>
        private static int CellLevel(ConnectionProfile profile)
        {
            try
            {
                byte? bars = profile.GetSignalBars();
                if (bars.HasValue) return Math.Max(1, Math.Min((int)bars.Value, 5));
            }
            catch { }
            return 0;
        }

        // ---- Native Wi-Fi (wlanapi.dll) ------------------------------------------------------------------

        private const uint WlanClientVersion = 2;                 // Vista and later
        private const int wlan_intf_opcode_current_connection = 7;
        private const int wlan_interface_state_connected = 1;

        [DllImport("wlanapi.dll")]
        private static extern uint WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

        [DllImport("wlanapi.dll")]
        private static extern uint WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

        [DllImport("wlanapi.dll")]
        private static extern uint WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

        [DllImport("wlanapi.dll")]
        private static extern uint WlanQueryInterface(IntPtr hClientHandle, ref Guid pInterfaceGuid, int OpCode, IntPtr pReserved,
                                                      out uint pdwDataSize, out IntPtr ppData, out int pWlanOpcodeValueType);

        [DllImport("wlanapi.dll")]
        private static extern void WlanFreeMemory(IntPtr pMemory);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_INTERFACE_INFO
        {
            public Guid InterfaceGuid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string strInterfaceDescription;
            public int isState;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DOT11_SSID
        {
            public uint uSSIDLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] ucSSID;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_ASSOCIATION_ATTRIBUTES
        {
            public DOT11_SSID dot11Ssid;
            public int dot11BssType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public byte[] dot11Bssid;
            public int dot11PhyType;
            public uint uDot11PhyIndex;
            public uint wlanSignalQuality;     // 0..100
            public uint ulRxRate;
            public uint ulTxRate;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_SECURITY_ATTRIBUTES
        {
            public int bSecurityEnabled;
            public int bOneXEnabled;
            public int dot11AuthAlgorithm;
            public int dot11CipherAlgorithm;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_CONNECTION_ATTRIBUTES
        {
            public int isState;
            public int wlanConnectionMode;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string strProfileName;
            public WLAN_ASSOCIATION_ATTRIBUTES wlanAssociationAttributes;
            public WLAN_SECURITY_ATTRIBUTES wlanSecurityAttributes;
        }

        /// <summary>
        /// Signal quality (0..100) of the Wi-Fi interface that belongs to <paramref name="profile"/>, or the first
        /// connected Wi-Fi interface. Every native handle and buffer is released before returning.
        /// </summary>
        private static int? QueryWlanSignalQuality(ConnectionProfile profile)
        {
            Guid? adapterId = null;
            try { if (profile.NetworkAdapter != null) adapterId = profile.NetworkAdapter.NetworkAdapterId; } catch { }

            IntPtr client = IntPtr.Zero;
            IntPtr list = IntPtr.Zero;
            try
            {
                if (WlanOpenHandle(WlanClientVersion, IntPtr.Zero, out _, out client) != 0) return null;

                if (WlanEnumInterfaces(client, IntPtr.Zero, out list) != 0 || list == IntPtr.Zero) return null;

                // WLAN_INTERFACE_INFO_LIST: DWORD dwNumberOfItems; DWORD dwIndex; WLAN_INTERFACE_INFO InterfaceInfo[];
                int count = Marshal.ReadInt32(list, 0);
                int stride = Marshal.SizeOf<WLAN_INTERFACE_INFO>();

                Guid? match = null;
                Guid? firstConnected = null;
                for (int i = 0; i < count; i++)
                {
                    var info = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(IntPtr.Add(list, 8 + i * stride));
                    if (adapterId.HasValue && info.InterfaceGuid == adapterId.Value) match = info.InterfaceGuid;
                    if (info.isState == wlan_interface_state_connected && !firstConnected.HasValue) firstConnected = info.InterfaceGuid;
                }

                Guid? target = match ?? firstConnected;
                if (!target.HasValue) return null;

                Guid guid = target.Value;
                IntPtr data = IntPtr.Zero;
                try
                {
                    if (WlanQueryInterface(client, ref guid, wlan_intf_opcode_current_connection, IntPtr.Zero,
                                           out _, out data, out _) != 0 || data == IntPtr.Zero)
                    {
                        return null;
                    }

                    var attributes = Marshal.PtrToStructure<WLAN_CONNECTION_ATTRIBUTES>(data);
                    if (attributes.isState != wlan_interface_state_connected) return null;
                    return (int)Math.Min(attributes.wlanAssociationAttributes.wlanSignalQuality, 100u);
                }
                finally
                {
                    if (data != IntPtr.Zero) WlanFreeMemory(data);
                }
            }
            catch (DllNotFoundException) { return null; }
            catch (EntryPointNotFoundException) { return null; }
            finally
            {
                if (list != IntPtr.Zero) WlanFreeMemory(list);
                if (client != IntPtr.Zero) WlanCloseHandle(client, IntPtr.Zero);
            }
        }
    }
}
