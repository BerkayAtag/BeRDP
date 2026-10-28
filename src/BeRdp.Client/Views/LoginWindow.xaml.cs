using System.IO;
using System.Net.Http;
using System.Security;
using System.Windows;
using BeRdp.Client.Models;
using BeRdp.Client.Services;

namespace BeRdp.Client.Views;

public partial class LoginWindow : Window
{
    private AppConfiguration? _configuration;
    private readonly bool _autoSso;
    private bool _loggingIn;
    private string? _resolvedBackendUrl;

    public LoginWindow() : this(true) { }

    public LoginWindow(bool autoSso)
    {
        InitializeComponent();
        _autoSso = autoSso;
        UpdateThemeButtonLabel();
        ThemeManager.ThemeChanged += UpdateThemeButtonLabel;
        Closed += (_, _) => { ThemeManager.ThemeChanged -= UpdateThemeButtonLabel; PasswordBox.Clear(); };

        try
        {
            _configuration = AppConfiguration.Load();
            Loaded += LoginWindow_OnLoaded;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or SecurityException or FormatException)
        {
            ShowError($"İstemci kurulumu yapılandırılamadı: {ex.Message}");
            LoginButton.IsEnabled = false;
            WindowsSsoButton.IsEnabled = false;
        }
    }

    private void UpdateThemeButtonLabel()
    {
        ThemeToggleButton.Content = ThemeManager.CurrentTheme == AppTheme.Dark ? "☀️ Açık" : "🌙 Koyu";
    }

    private void ThemeToggleButton_OnClick(object sender, RoutedEventArgs e)
    {
        ThemeManager.ToggleTheme();
    }

    private async void LoginWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_autoSso && _configuration?.UseWindowsSso == true)
        {
            await LoginWithWindowsAsync();
        }
        else
        {
            UsernameTextBox.Focus();
        }
    }

    public void ShowSessionMessage(string message) => ShowError(message);

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorContainer.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowInfo(string message)
    {
        InfoTextBlock.Text = message;
        InfoContainer.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// The installer may receive the Admin Console address (for example
    /// http://localhost:5173). The client talks to the API, so resolve the
    /// backend address from the portal configuration before the first call.
    /// </summary>
    private async Task<string> ResolveBackendBaseUrlAsync()
    {
        if (_resolvedBackendUrl is not null)
        {
            return _resolvedBackendUrl;
        }

        var endpoint = await BackendEndpointResolver.ResolveAsync(_configuration!.BackendBaseUrl);
        _resolvedBackendUrl = endpoint.Address.AbsoluteUri.TrimEnd('/');
        if (endpoint.WasCorrected && endpoint.Notice is not null)
        {
            ShowInfo(endpoint.Notice);
        }

        return _resolvedBackendUrl;
    }

    private async void WindowsSsoButton_OnClick(object sender, RoutedEventArgs e)
        => await LoginWithWindowsAsync();

    private async void LoginButton_OnClick(object sender, RoutedEventArgs e)
        => await LoginWithPasswordAsync();

    private async Task LoginWithWindowsAsync()
    {
        if (_loggingIn || _configuration is null) { return; }
        _loggingIn = true;
        ShowError(string.Empty);
        SetButtonsEnabled(false);
        ApiClient? apiClient = null;

        try
        {
            var backendBaseUrl = await ResolveBackendBaseUrlAsync();
            apiClient = new ApiClient(backendBaseUrl);
            var session = await apiClient.LoginWithWindowsAsync();
            ProceedToMainWindow(apiClient, session, new SessionCredential
            {
                InputUsername = session.Username,
                Password = string.Empty,
                UseWindowsSso = true
            });
            apiClient = null;
        }
        catch (Exception ex)
        {
            ShowError(GetFriendlyErrorMessage(ex));
            UsernameTextBox.Focus();
        }
        finally
        {
            SetButtonsEnabled(true);
            _loggingIn = false;
            apiClient?.Dispose();
        }
    }

    private async Task LoginWithPasswordAsync()
    {
        if (_loggingIn || _configuration is null) { return; }
        var username = UsernameTextBox.Text.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ShowError("Kullanıcı adı ve parola zorunludur.");
            return;
        }

        _loggingIn = true;
        ShowError(string.Empty);
        SetButtonsEnabled(false);
        ApiClient? apiClient = null;

        try
        {
            var backendBaseUrl = await ResolveBackendBaseUrlAsync();
            apiClient = new ApiClient(backendBaseUrl);
            var session = await apiClient.LoginAsync(username, password);
            ProceedToMainWindow(apiClient, session, new SessionCredential
            {
                InputUsername = username,
                Password = password,
                UseWindowsSso = false
            });
            apiClient = null;
            PasswordBox.Clear();
        }
        catch (Exception ex)
        {
            ShowError(GetFriendlyErrorMessage(ex));
        }
        finally
        {
            SetButtonsEnabled(true);
            _loggingIn = false;
            apiClient?.Dispose();
        }
    }

    private void ProceedToMainWindow(ApiClient apiClient, LoginResponse session, SessionCredential credential)
    {
        var mainWindow = new MainWindow(apiClient, session, credential, _configuration?.PollingSeconds ?? 30);
        System.Windows.Application.Current.MainWindow = mainWindow;
        mainWindow.Show();
        Close();
    }

    private void SetButtonsEnabled(bool enabled)
    {
        WindowsSsoButton.IsEnabled = enabled;
        LoginButton.IsEnabled = enabled;
    }

    private static string GetFriendlyErrorMessage(Exception ex) => ex switch
    {
        ApiException apiEx => apiEx.Message,
        InvalidOperationException invEx => invEx.Message,
        HttpRequestException httpEx => $"Backend erişimi başarısız: {httpEx.Message}",
        TaskCanceledException => "Sunucu isteği zaman aşımına uğradı. Adres ve firewall erişimini kontrol edin.",
        _ => $"Giriş yapılamadı: {ex.Message}"
    };
}
