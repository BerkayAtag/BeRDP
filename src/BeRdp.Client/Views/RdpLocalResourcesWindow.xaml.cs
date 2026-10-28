using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BeRdp.Client.Models;
using BeRdp.Client.Services;

namespace BeRdp.Client.Views;

public partial class RdpLocalResourcesWindow : Window
{
    private readonly List<string> _selectionOrder = [];
    private bool _displayReady;
    public MachineRdpLocalResourceOptions SelectedOptions { get; private set; }
    public ObservableCollection<RdpMonitorViewModel> Monitors { get; } = [];

    public RdpLocalResourcesWindow(string machineName, MachineRdpLocalResourceOptions initialOptions)
    {
        InitializeComponent();
        SelectedOptions = initialOptions.Clone();
        DataContext = this;
        MachineTitleText.Text = $"Makine Ayarları - {machineName}";
        var active = MonitorCatalog.GetActiveMonitors();
        var devices = SelectedOptions.SelectedMonitorDevices ?? [];
        var savedIds = SelectedOptions.SelectedMonitorIds ?? [];
        _selectionOrder.AddRange(devices.Where(device => active.Any(monitor =>
            string.Equals(device, monitor.DeviceName, StringComparison.OrdinalIgnoreCase))));
        foreach (var monitor in active)
        {
            monitor.IsSelected = devices.Count > 0
                ? _selectionOrder.Contains(monitor.DeviceName, StringComparer.OrdinalIgnoreCase)
                : savedIds.Count > 0 ? savedIds.Contains(monitor.Id) : monitor.IsPrimary;
            if (monitor.IsSelected && !_selectionOrder.Contains(monitor.DeviceName))
            {
                _selectionOrder.Add(monitor.DeviceName);
            }
            monitor.PropertyChanged += Monitor_OnPropertyChanged;
            Monitors.Add(monitor);
        }
        UseAllMonitorsCheckBox.IsChecked = SelectedOptions.UseAllMonitors;
        CustomResolutionCheckBox.IsChecked = SelectedOptions.UseCustomResolution;
        DesktopWidthTextBox.Text = SelectedOptions.DesktopWidth.ToString();
        DesktopHeightTextBox.Text = SelectedOptions.DesktopHeight.ToString();
        ClipboardCheckBox.IsChecked = SelectedOptions.RedirectClipboard;
        PrintersCheckBox.IsChecked = SelectedOptions.RedirectPrinters;
        SmartCardsCheckBox.IsChecked = SelectedOptions.RedirectSmartCards;
        DrivesCheckBox.IsChecked = SelectedOptions.RedirectDrives;
        ComPortsCheckBox.IsChecked = SelectedOptions.RedirectComPorts;
        PnPDevicesCheckBox.IsChecked = SelectedOptions.RedirectPnPDevices;
        _displayReady = true;
        UpdateDisplayControls();
        if (devices.Any(device => !_selectionOrder.Contains(device, StringComparer.OrdinalIgnoreCase)))
        {
            DisplayValidationText.Text = "Kaydedilen bazı monitörler artık bağlı değil. Bu makine için ekranları yeniden seçip kaydedin.";
        }
    }

    private void Monitor_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is RdpMonitorViewModel monitor && e.PropertyName == nameof(monitor.IsSelected))
        {
            _selectionOrder.RemoveAll(device => string.Equals(device, monitor.DeviceName, StringComparison.OrdinalIgnoreCase));
            if (monitor.IsSelected)
            {
                _selectionOrder.Add(monitor.DeviceName);
            }
            UpdateDisplayControls();
        }
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        var options = new MachineRdpLocalResourceOptions
        {
            UseAllMonitors = UseAllMonitorsCheckBox.IsChecked == true,
            SelectedMonitorDevices = [.. _selectionOrder],
            SelectedMonitorIds = _selectionOrder.Select(device => Monitors.Single(x => x.DeviceName == device).Id).ToList(),
            UseCustomResolution = CustomResolutionCheckBox.IsChecked == true,
            DesktopWidth = SelectedOptions.DesktopWidth,
            DesktopHeight = SelectedOptions.DesktopHeight,
            RedirectClipboard = ClipboardCheckBox.IsChecked == true,
            RedirectPrinters = PrintersCheckBox.IsChecked == true,
            RedirectSmartCards = SmartCardsCheckBox.IsChecked == true,
            RedirectDrives = DrivesCheckBox.IsChecked == true,
            RedirectComPorts = ComPortsCheckBox.IsChecked == true,
            RedirectPnPDevices = PnPDevicesCheckBox.IsChecked == true
        };
        if (!options.UseAllMonitors && _selectionOrder.Count == 0)
        {
            DisplayValidationText.Text = "En az bir monitör seçin.";
            return;
        }
        if (options.UseCustomResolution)
        {
            if (!int.TryParse(DesktopWidthTextBox.Text, out var width) ||
                !int.TryParse(DesktopHeightTextBox.Text, out var height))
            {
                DisplayValidationText.Text = "Çözünürlük değerleri 200-8192 arasında tam sayı olmalıdır.";
                return;
            }
            options.DesktopWidth = width;
            options.DesktopHeight = height;
        }
        try
        {
            RdpDisplaySettings.ValidateSelection(options, RdpDisplaySettings.Resolve(options, Monitors.ToList()));
            SelectedOptions = options;
            DialogResult = true;
        }
        catch (InvalidOperationException ex)
        {
            DisplayValidationText.Text = ex.Message;
        }
    }

    private void DisplaySelection_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_displayReady)
        {
            UpdateDisplayControls();
        }
    }

    private void UpdateDisplayControls()
    {
        var useAll = UseAllMonitorsCheckBox.IsChecked == true;
        MonitorsItemsControl.IsEnabled = !useAll;
        DesktopWidthTextBox.IsEnabled = CustomResolutionCheckBox.IsChecked == true;
        DesktopHeightTextBox.IsEnabled = CustomResolutionCheckBox.IsChecked == true;
        DisplayValidationText.Text = useAll
            ? "Oturum tüm aktif monitörlere yayılacak."
            : "İlk seçilen ekran uzak oturumun ana ekranıdır. Seçilen ekranların bitişik olması gerekir.";
        DrawMonitorLayout();
    }

    private void DrawMonitorLayout()
    {
        MonitorLayoutCanvas.Children.Clear();
        var left = Monitors.Min(x => x.Left);
        var top = Monitors.Min(x => x.Top);
        var totalWidth = Monitors.Max(x => x.Left + x.Width) - left;
        var totalHeight = Monitors.Max(x => x.Top + x.Height) - top;
        var scale = Math.Min(560.0 / totalWidth, 130.0 / totalHeight);
        foreach (var monitor in Monitors)
        {
            var button = new Button
            {
                Content = $"Ekran {monitor.Id + 1}\n{monitor.Width} x {monitor.Height}",
                Width = Math.Max(24, monitor.Width * scale - 4),
                Height = Math.Max(24, monitor.Height * scale - 4),
                Background = monitor.IsSelected || UseAllMonitorsCheckBox.IsChecked == true
                    ? new SolidColorBrush(Color.FromRgb(0, 120, 212))
                    : new SolidColorBrush(Color.FromRgb(63, 70, 89)),
                Foreground = Brushes.White,
                IsEnabled = UseAllMonitorsCheckBox.IsChecked != true,
                ToolTip = monitor.DisplayText,
                Tag = monitor
            };
            button.Click += (_, _) => monitor.IsSelected = !monitor.IsSelected;
            Canvas.SetLeft(button, 8 + (monitor.Left - left) * scale);
            Canvas.SetTop(button, 8 + (monitor.Top - top) * scale);
            MonitorLayoutCanvas.Children.Add(button);
        }
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
