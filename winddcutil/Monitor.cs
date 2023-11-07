using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace winddcutil
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    static class MonitorPInvoke
    {
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

    public class Monitor
    {
        protected string Identifier { get; set; } = "";
        protected IntPtr Handle { get; set; } = IntPtr.Zero;

        public uint GetBrightness()
        {
            uint minBrightness = 0;
            uint currentBrightness = 0;
            uint maxBrightness = 0;
            MonitorPInvoke.GetMonitorBrightness(Handle, ref minBrightness, ref currentBrightness, ref maxBrightness);

            return currentBrightness;
        }

        Task setBrightnessTask = Task.CompletedTask;
        uint targetBrightness = 0;
        public void SetBrightness(uint value)
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

        public static async Task<List<Monitor>?> Detect()
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
                                newMonitors.Add(new Monitor(p.hPhysicalMonitor, p.szPhysicalMonitorDescription));

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
                return null;
            }
        }

        public Monitor(IntPtr handle, string identifier)
        {
            Handle = handle;
            Identifier = identifier;
        }

        public Monitor(Monitor physicalMon)
        {
            Handle = physicalMon.Handle;
            Identifier = physicalMon.Identifier;
        }
    }
}