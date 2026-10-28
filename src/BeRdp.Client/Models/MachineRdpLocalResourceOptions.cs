namespace BeRdp.Client.Models;

public sealed class MachineRdpLocalResourceOptions
{
    public bool UseAllMonitors { get; set; }
    public List<int> SelectedMonitorIds { get; set; } = [];
    public List<string> SelectedMonitorDevices { get; set; } = [];
    public bool UseCustomResolution { get; set; }
    public int DesktopWidth { get; set; } = 1920;
    public int DesktopHeight { get; set; } = 1080;
    public bool RedirectClipboard { get; set; } = true;
    public bool RedirectPrinters { get; set; }
    public bool RedirectSmartCards { get; set; }
    public bool RedirectDrives { get; set; }
    public bool RedirectComPorts { get; set; }
    public bool RedirectPnPDevices { get; set; }

    public MachineRdpLocalResourceOptions Clone()
    {
        return new MachineRdpLocalResourceOptions
        {
            UseAllMonitors = UseAllMonitors,
            SelectedMonitorIds = SelectedMonitorIds is null ? [] : [.. SelectedMonitorIds],
            SelectedMonitorDevices = SelectedMonitorDevices is null ? [] : [.. SelectedMonitorDevices],
            UseCustomResolution = UseCustomResolution,
            DesktopWidth = DesktopWidth,
            DesktopHeight = DesktopHeight,
            RedirectClipboard = RedirectClipboard,
            RedirectPrinters = RedirectPrinters,
            RedirectSmartCards = RedirectSmartCards,
            RedirectDrives = RedirectDrives,
            RedirectComPorts = RedirectComPorts,
            RedirectPnPDevices = RedirectPnPDevices
        };
    }
}
