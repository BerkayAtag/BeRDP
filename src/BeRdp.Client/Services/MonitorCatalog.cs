using System.ComponentModel;
using System.Runtime.InteropServices;
using BeRdp.Client.Models;

namespace BeRdp.Client.Services;

public static class MonitorCatalog
{
    public static IReadOnlyList<RdpMonitorViewModel> GetActiveMonitors()
    {
        var monitors = new List<RdpMonitorViewModel>();
        var error = 0;
        MonitorEnumProc callback = (IntPtr handle, IntPtr deviceContext, ref NativeRect bounds, IntPtr data) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>(), DeviceName = string.Empty };
            if (!GetMonitorInfo(handle, ref info))
            {
                error = Marshal.GetLastWin32Error();
                return false;
            }

            monitors.Add(new RdpMonitorViewModel
            {
                Id = monitors.Count,
                DeviceName = info.DeviceName,
                Left = info.Monitor.Left,
                Top = info.Monitor.Top,
                Width = info.Monitor.Right - info.Monitor.Left,
                Height = info.Monitor.Bottom - info.Monitor.Top,
                IsPrimary = (info.Flags & 1) != 0,
                WorkLeft = info.Work.Left,
                WorkTop = info.Work.Top,
                WorkWidth = info.Work.Right - info.Work.Left,
                WorkHeight = info.Work.Bottom - info.Work.Top
            });
            return true;
        };
        if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero))
        {
            throw new Win32Exception(error != 0 ? error : Marshal.GetLastWin32Error(), "Aktif monitörler okunamadı.");
        }
        if (monitors.Count == 0)
        {
            throw new InvalidOperationException("Aktif monitör bulunamadı.");
        }

        return monitors;
    }

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr deviceContext, ref NativeRect rect, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr deviceContext, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
