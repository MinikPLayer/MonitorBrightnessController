using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace winddcutil
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct MONITORINFOEX
    {
        public int Size;
        public Rect Monitor;
        public Rect WorkArea;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    [Flags()]
    public enum DisplayDeviceStateFlags : int
    {
        /// <summary>The device is part of the desktop.</summary>
        AttachedToDesktop = 0x1,
        MultiDriver = 0x2,
        /// <summary>The device is part of the desktop.</summary>
        PrimaryDevice = 0x4,
        /// <summary>Represents a pseudo device used to mirror application drawing for remoting or other purposes.</summary>
        MirroringDriver = 0x8,
        /// <summary>The device is VGA compatible.</summary>
        VGACompatible = 0x10,
        /// <summary>The device is removable; it cannot be the primary display.</summary>
        Removable = 0x20,
        /// <summary>The device has more display modes than its output devices support.</summary>
        ModesPruned = 0x8000000,
        Remote = 0x4000000,
        Disconnect = 0x2000000
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct DISPLAY_DEVICE
    {
        [MarshalAs(UnmanagedType.U4)]
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        [MarshalAs(UnmanagedType.U4)]
        public DisplayDeviceStateFlags StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    static class MonitorPInvoke
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);


        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("dxva2.dll", EntryPoint = "GetNumberOfPhysicalMonitorsFromHMONITOR")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, ref uint pdwNumberOfPhysicalMonitors);

        [DllImport("dxva2.dll", EntryPoint = "GetPhysicalMonitorsFromHMONITOR")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        public delegate bool EnumMonitorsDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsDelegate lpfnEnum, IntPtr dwData);

        [DllImport("dxva2.dll", EntryPoint = "GetMonitorBrightness")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorBrightness(IntPtr handle, ref uint minimumBrightness, ref uint currentBrightness, ref uint maxBrightness);

        [DllImport("dxva2.dll", EntryPoint = "SetMonitorBrightness")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetMonitorBrightness(IntPtr handle, uint newBrightness);

    }

    public class MonitorDDC : Monitor
    {
        protected string Identifier { get; set; } = "";
        protected IntPtr Handle { get; set; } = IntPtr.Zero;

        public override uint GetBrightness()
        {
            uint minBrightness = 0;
            uint currentBrightness = 0;
            uint maxBrightness = 0;
            MonitorPInvoke.GetMonitorBrightness(Handle, ref minBrightness, ref currentBrightness, ref maxBrightness);

            return currentBrightness;
        }

        Task setBrightnessTask = Task.CompletedTask;
        uint targetBrightness = 0;
        public override void SetBrightness(uint value)
        {
            targetBrightness = value;
            if (setBrightnessTask.IsCompleted)
            {
                setBrightnessTask = Task.Run(() =>
                {
                    MonitorPInvoke.SetMonitorBrightness(Handle, targetBrightness);
                    if (value != targetBrightness)
                        setBrightnessTask.ContinueWith(setBrightnessTask => SetBrightness(targetBrightness));
                });
            }
        }

        public override string ToString()
        {
            return Identifier;
        }

        public static new async Task<List<Monitor>> Detect()
        {
            try
            {
                return await Task.Run(() =>
                {
                    var newMonitors = new ConcurrentBag<Monitor>();
                    var enumRet = MonitorPInvoke.EnumDisplayMonitors(
                        IntPtr.Zero,
                        IntPtr.Zero,
                        (IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr data) =>
                        {
                            uint numberOfPhysicalMonitors = 0;
                            if (!MonitorPInvoke.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, ref numberOfPhysicalMonitors))
                                throw new Exception("Cannot get number of physical monitors");

                            var physicalMonitors = new PHYSICAL_MONITOR[numberOfPhysicalMonitors];
                            if (!MonitorPInvoke.GetPhysicalMonitorsFromHMONITOR(hMonitor, numberOfPhysicalMonitors, physicalMonitors))
                                throw new Exception("Cannot get physical monitors");

                            foreach (var p in physicalMonitors)
                            {
                                var info = new MONITORINFOEX();
                                info.Size = Marshal.SizeOf(typeof(MONITORINFOEX));
                                if(!MonitorPInvoke.GetMonitorInfo(hMonitor, ref info))
                                    throw new Exception("Cannot get monitor info");

                                newMonitors.Add(new MonitorDDC(p.hPhysicalMonitor, p.szPhysicalMonitorDescription));
                            }
                            return true;
                        },
                        IntPtr.Zero
                    );

                    if (!enumRet)
                        throw new Exception("Cannot enum display monitors");

                    return newMonitors.ToList();
                });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return new List<Monitor>();
            }
        }

        public MonitorDDC(IntPtr handle, string identifier)
        {
            Handle = handle;
            Identifier = identifier;
        }

        public MonitorDDC(MonitorDDC physicalMon)
        {
            Handle = physicalMon.Handle;
            Identifier = physicalMon.Identifier;
        }
    }
}