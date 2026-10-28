using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BeRdp.Client.Models;

public sealed class RdpMonitorViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public required int Id { get; init; }
    public required string DeviceName { get; init; }
    public required int Left { get; init; }
    public required int Top { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required bool IsPrimary { get; init; }
    public int WorkLeft { get; init; }
    public int WorkTop { get; init; }
    public int WorkWidth { get; init; }
    public int WorkHeight { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public string DisplayText =>
        $"Ekran {Id + 1}{(IsPrimary ? " (Ana ekran)" : string.Empty)} - {Width} x {Height} - Konum: {Left}, {Top}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
