using System.Windows;

namespace BeRdp.Client.Views;

public partial class RdpCredentialPromptWindow : Window
{
    public string EnteredPassword { get; private set; } = string.Empty;
    public bool RememberForSession { get; private set; }
    public bool UseMstscPrompt { get; private set; }
    public bool Confirmed { get; private set; }

    public RdpCredentialPromptWindow(string domainUsername)
    {
        InitializeComponent();
        UsernameDisplay.Text = domainUsername;
        Loaded += (_, _) => PasswordInput.Focus();
    }

    private void ConnectDirectButton_OnClick(object sender, RoutedEventArgs e)
    {
        var password = PasswordInput.Password;
        if (string.IsNullOrEmpty(password))
        {
            ErrorText.Text = "Lütfen Active Directory parolanızı girin veya 'RDP'de Sor' seçeneğini kullanın.";
            ErrorText.Visibility = Visibility.Visible;
            PasswordInput.Focus();
            return;
        }

        EnteredPassword = password;
        RememberForSession = RememberSessionCheckBox.IsChecked == true;
        UseMstscPrompt = false;
        Confirmed = true;
        DialogResult = true;
        Close();
    }

    private void PromptInMstscButton_OnClick(object sender, RoutedEventArgs e)
    {
        UseMstscPrompt = true;
        Confirmed = true;
        DialogResult = true;
        Close();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        DialogResult = false;
        Close();
    }
}
