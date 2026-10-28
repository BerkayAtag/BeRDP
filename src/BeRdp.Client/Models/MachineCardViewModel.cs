using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using BeRdp.Client.Services;

namespace BeRdp.Client.Models;

public class MachineCardViewModel : INotifyPropertyChanged
{
    public Guid Id { get; }
    public string Name { get; }
    public string IpAddress { get; }
    public string? RdpHostName { get; }
    public int RdpPort { get; }
    public bool IsOnline { get; }
    public string EndpointText => $"{RdpHostName ?? IpAddress}:{RdpPort}";
    public string StatusText => IsOnline ? "Erişilebilir" : "Erişilemiyor";

    public Brush StatusBackground => IsOnline
        ? (Brush)System.Windows.Application.Current.Resources["Brush.Theme.BadgeOnlineBackground"]
        : (Brush)System.Windows.Application.Current.Resources["Brush.Theme.BadgeOfflineBackground"];

    public Brush StatusForeground => IsOnline
        ? (Brush)System.Windows.Application.Current.Resources["Brush.Theme.BadgeOnlineForeground"]
        : (Brush)System.Windows.Application.Current.Resources["Brush.Theme.BadgeOfflineForeground"];

    public MachineCardViewModel(MachineDto machine)
    {
        Id = machine.Id;
        Name = machine.Name;
        IpAddress = machine.IpAddress;
        RdpHostName = machine.RdpHostName;
        RdpPort = machine.RdpPort;
        IsOnline = machine.IsOnline;

    }

    public void RefreshTheme()
    {
        OnPropertyChanged(nameof(StatusBackground));
        OnPropertyChanged(nameof(StatusForeground));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
