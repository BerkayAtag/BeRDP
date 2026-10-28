using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using BeRdp.Client.Models;
using BeRdp.Client.Services;
using BeRdp.Client.Views;
using Microsoft.AspNetCore.SignalR.Client;

namespace BeRdp.Client;

public partial class MainWindow : Window
{
    private readonly ApiClient _apiClient;
    private readonly LoginResponse _session;
    private readonly SessionCredential _sessionCredential;
    private readonly RdpOptionStore _rdpOptionStore = new();
    private readonly RdpCredentialStore _credentials = new();
    private readonly DispatcherTimer _pollingTimer;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _refreshGate = new(1);
    private readonly SemaphoreSlim _hubStartGate = new(1);
    private HubConnection? _hubConnection;
    private bool _closing;
    private bool _canClose;
    private bool _returnToLogin;
    private string? _sessionMessage;

    public ObservableCollection<MachineCardViewModel> Machines { get; } = [];
    public ObservableCollection<MachineCardViewModel> FilteredMachines { get; } = [];

    public MainWindow(ApiClient apiClient, LoginResponse session, SessionCredential sessionCredential, int pollingSeconds)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _session = session;
        _sessionCredential = sessionCredential;
        DataContext = this;

        var displayUser = !string.IsNullOrWhiteSpace(session.DisplayName)
            ? $"{session.DisplayName} ({session.Username})"
            : session.Username;
        HeaderText.Text = $"{displayUser} · Atanan Sanal Masaüstleri";

        UpdateThemeButtonLabel();
        ThemeManager.ThemeChanged += UpdateThemeButtonLabel;
        Closed += (_, _) => ThemeManager.ThemeChanged -= UpdateThemeButtonLabel;

        _pollingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(pollingSeconds)
        };
        _pollingTimer.Tick += PollingTimerOnTick;
        Loaded += MainWindow_OnLoaded;
        Closing += MainWindow_OnClosing;
    }

    private void UpdateThemeButtonLabel()
    {
        foreach (var machine in Machines) machine.RefreshTheme();
        ThemeToggleButton.Content = ThemeManager.CurrentTheme == AppTheme.Dark ? "☀️ Açık" : "🌙 Koyu";
    }

    private void ThemeToggleButton_OnClick(object sender, RoutedEventArgs e)
    {
        ThemeManager.ToggleTheme();
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshMachinesAsync();
        if (_closing) { return; }
        _pollingTimer.Start();
        await StartHubAsync();
        if (_rdpOptionStore.LoadWarning is not null)
        {
            ShowError(_rdpOptionStore.LoadWarning);
        }
    }

    private async void PollingTimerOnTick(object? sender, EventArgs e)
    {
        await RefreshMachinesAsync();
        await StartHubAsync();
    }

    private async void RefreshButton_OnClick(object sender, RoutedEventArgs e) => await RefreshMachinesAsync();

    private async Task RefreshMachinesAsync()
    {
        if (_closing || !await _refreshGate.WaitAsync(0)) { return; }
        try
        {
            var machines = await _apiClient.GetMachinesAsync(_session.Token, _lifetime.Token);
            if (_closing) { return; }
            HideError();
            Machines.Clear();
            foreach (var machine in machines.OrderBy(x => x.Name))
            {
                Machines.Add(new MachineCardViewModel(machine));
            }
            ApplyFilter();
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            ShowError(ex.Message);
            _sessionMessage = ex.Message;
            _returnToLogin = true;
            Close();
        }
        catch (InvalidOperationException ex)
        {
            ShowError(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            ShowError($"Backend erişimi başarısız: {ex.Message}");
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (OperationCanceledException)
        {
            ShowError("Sunucu isteği zaman aşımına uğradı. Adres ve firewall erişimini kontrol edin.");
        }
        finally { _refreshGate.Release(); }
    }

    private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }
    private void SearchBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        // If the filter matches exactly one machine, initiate a direct connection to it
        if (FilteredMachines.Count == 1)
        {
            var machine = FilteredMachines[0];
            var button = new Button { Tag = machine };
            ConnectButton_OnClick(button, new RoutedEventArgs());
        }
    }


    private void ApplyFilter()
    {
        var filter = SearchBox?.Text?.Trim().ToLowerInvariant() ?? string.Empty;
        FilteredMachines.Clear();

        foreach (var m in Machines)
        {
            if (string.IsNullOrEmpty(filter) ||
                m.Name.ToLowerInvariant().Contains(filter) ||
                m.IpAddress.ToLowerInvariant().Contains(filter) ||
                (m.RdpHostName != null && m.RdpHostName.ToLowerInvariant().Contains(filter)))
            {
                FilteredMachines.Add(m);
            }
        }

        MachineCountText.Text = $"{FilteredMachines.Count} / {Machines.Count} makine";
        EmptyStatePanel.Visibility = FilteredMachines.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task StartHubAsync()
    {
        if (_closing || !await _hubStartGate.WaitAsync(0)) { return; }
        try
        {
            if (_hubConnection is null)
            {
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(new Uri(_apiClient.BaseAddress, "hubs/assignments"), options =>
                    {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(_session.Token);
                    })
                    .WithAutomaticReconnect()
                    .Build();
                _hubConnection.On("AssignmentsChanged", () =>
                {
                    if (!_closing)
                    {
                        _ = Dispatcher.InvokeAsync(async () => await RefreshMachinesAsync());
                    }
                });
            }
            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                await _hubConnection.StartAsync(_lifetime.Token);
            }
        }
        catch (HttpRequestException ex)
        {
            if (!_closing)
            {
                ShowError($"Canlı güncelleme kanalı kurulamadı; liste periyodik yenileniyor: {ex.Message}");
            }
        }
        catch (InvalidOperationException ex)
        {
            Trace.TraceWarning($"SignalR startup failed: {ex.Message}");
            if (!_closing) { ShowError($"Canlı kanal başlatılamadı: {ex.Message}"); }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (OperationCanceledException)
        {
            if (!_closing) { ShowError("Canlı kanal zaman aşımına uğradı; sonraki yenilemede tekrar denenecek."); }
        }
        finally { _hubStartGate.Release(); }
    }

    private async void ConnectButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_closing || sender is not FrameworkElement { Tag: MachineCardViewModel machine }) { return; }
        IReadOnlyList<string> targets = [];
        string? filePath = null;
        var tracked = false;
        try
        {
            HideError();
            machine = new MachineCardViewModel(await _apiClient.GetAssignedMachineAsync(machine.Id, _session.Token, _lifetime.Token));
            var options = _rdpOptionStore.Get(machine.Id);
            var username = ResolveRdpUsername();
            var address = machine.RdpHostName ?? machine.IpAddress;

            string passwordToUse = _sessionCredential.Password;
            bool promptInMstsc = false;

            // If we do not have an AD password in memory (e.g. logged in with Windows Hello SSO):
            if (string.IsNullOrEmpty(passwordToUse))
            {
                var promptDialog = new RdpCredentialPromptWindow(username) { Owner = this };
                var result = promptDialog.ShowDialog();
                if (result != true || !promptDialog.Confirmed)
                {
                    return; // User cancelled
                }

                if (promptDialog.UseMstscPrompt)
                {
                    promptInMstsc = true;
                }
                else
                {
                    passwordToUse = promptDialog.EnteredPassword;
                    if (promptDialog.RememberForSession)
                    {
                        _sessionCredential.Password = passwordToUse;
                    }
                }
            }

            var content = RdpProfileWriter.Build(
                address,
                machine.RdpPort,
                username,
                options,
                MonitorCatalog.GetActiveMonitors(),
                useWindowsSso: false,
                promptForCredentials: promptInMstsc
            );

            var directory = Path.Combine(Path.GetTempPath(), "BeRDP", "RdpSessions");
            Directory.CreateDirectory(directory);
            filePath = Path.Combine(directory, $"beRdp-{machine.Id:N}-{Guid.NewGuid():N}.rdp");
            File.WriteAllText(filePath, content, Encoding.Unicode);

            if (!promptInMstsc && !string.IsNullOrEmpty(passwordToUse))
            {
                var endpoint = RdpProfileWriter.Endpoint(address, machine.RdpPort);
                targets = await _credentials.AcquireAsync([address, endpoint], username, passwordToUse, _lifetime.Token);
            }

            _lifetime.Token.ThrowIfCancellationRequested();

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "mstsc.exe"),
                Arguments = $"\"{filePath}\"",
                UseShellExecute = false
            }) ?? throw new InvalidOperationException("RDP başlatılamadı.");

            tracked = true;
            _ = TrackRdpProcessExitAsync(process, targets, filePath);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or IOException or UnauthorizedAccessException or HttpRequestException)
        {
            ShowError($"RDP başlatılamadı: {ex.Message}");
        }
        catch (OperationCanceledException) { if (!_closing) ShowError("Bağlantı doğrulaması zaman aşımına uğradı."); }
        finally
        {
            if (!tracked)
            {
                if (filePath is not null) { DeleteTemporaryFile(filePath); }
                try { await _credentials.ReleaseAsync(targets); }
                catch (InvalidOperationException ex) { ShowError(ex.Message); }
            }
        }
    }

    private void MachineSettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_closing || sender is not FrameworkElement { Tag: MachineCardViewModel machine }) { return; }
        try
        {
            var window = new RdpLocalResourcesWindow(machine.Name, _rdpOptionStore.Get(machine.Id)) { Owner = this };
            if (window.ShowDialog() == true)
            {
                _rdpOptionStore.Save(machine.Id, window.SelectedOptions);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or IOException or UnauthorizedAccessException or HttpRequestException)
        {
            ShowError($"RDP ayarları kullanılamadı: {ex.Message}");
        }
    }

    private void LogoutButton_OnClick(object sender, RoutedEventArgs e)
    {
        _returnToLogin = true;
        Close();
    }

    private async void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (_canClose) { return; }
        e.Cancel = true;
        if (_closing) { return; }
        _closing = true;
        _pollingTimer.Stop();
        _lifetime.Cancel();
        await _refreshGate.WaitAsync();
        _refreshGate.Release();
        await _hubStartGate.WaitAsync();
        try
        {
            if (_hubConnection is not null) { await _hubConnection.DisposeAsync(); _hubConnection = null; }
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or OperationCanceledException)
        {
            Trace.TraceWarning($"SignalR shutdown failed: {ex.Message}");
        }
        finally { _hubStartGate.Release(); }
        try
        {
            await _credentials.ReleaseAllAsync();
        }
        catch (InvalidOperationException ex)
        {
            ShowError(ex.Message + " Temizliği tekrar denemek için pencereyi yeniden kapatın.");
            _closing = false;
            return;
        }
        _sessionCredential.Password = string.Empty;
        _apiClient.Dispose();
        if (_returnToLogin)
        {
            var login = new LoginWindow(autoSso: false);
            if (_sessionMessage is not null) { login.ShowSessionMessage(_sessionMessage); }
            System.Windows.Application.Current.MainWindow = login;
            login.Show();
        }
        _canClose = true;
        Close();
    }

    private async Task TrackRdpProcessExitAsync(Process process, IReadOnlyList<string> targets, string filePath)
    {
        try { await process.WaitForExitAsync(); }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            Trace.TraceWarning($"RDP process tracking failed: {ex.Message}");
        }
        finally
        {
            process.Dispose();
            DeleteTemporaryFile(filePath);
            try { await _credentials.ReleaseAsync(targets); }
            catch (InvalidOperationException ex)
            {
                Trace.TraceWarning(ex.Message);
                if (!_closing && !Dispatcher.HasShutdownStarted)
                {
                    await Dispatcher.InvokeAsync(() => ShowError(ex.Message));
                }
            }
        }
    }

    private string ResolveRdpUsername()
    {
        if (!string.IsNullOrWhiteSpace(_session.RdpUsername)) { return _session.RdpUsername; }
        var input = _sessionCredential.InputUsername.Trim();
        if (input.Contains('\\') || input.Contains('@')) { return input; }
        if (_session.Username.Contains('\\') || _session.Username.Contains('@')) { return _session.Username; }
        return _session.Username;
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorBanner.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void HideError()
    {
        ErrorTextBlock.Text = string.Empty;
        ErrorBanner.Visibility = Visibility.Collapsed;
    }

    private static void DeleteTemporaryFile(string filePath)
    {
        try { File.Delete(filePath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Temporary RDP file could not be deleted: {filePath}. {ex.Message}");
        }
    }
}
