using System.IO;
using System.Windows;
using System.Windows.Media;

namespace BeRdp.Client.Services;

public enum AppTheme
{
    Dark,
    Light
}

public static class ThemeManager
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BeRDP", "Client", "theme.txt");

    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    public static event Action? ThemeChanged;

    public static void Initialize()
    {
        var savedTheme = LoadSavedTheme();
        ApplyTheme(savedTheme, persist: false);
    }

    public static void ToggleTheme()
    {
        var next = CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
        ApplyTheme(next, persist: true);
    }

    public static void ApplyTheme(AppTheme theme, bool persist = true)
    {
        CurrentTheme = theme;
        var resources = Application.Current?.Resources;
        if (resources is null) { return; }

        // BeRDP accent red: #E20613
        var accentRed = Color.FromRgb(226, 6, 19);
        var white = Colors.White;

        if (theme == AppTheme.Dark)
        {
            // Dark Theme - Deep Modern Charcoal & Vibrant BeRDP Red
            SetBrush(resources, "Brush.Theme.Background", Color.FromRgb(17, 18, 21));
            SetBrush(resources, "Brush.Theme.TopBarBackground", Color.FromRgb(24, 25, 30));
            SetBrush(resources, "Brush.Theme.TopBarBorder", Color.FromRgb(40, 42, 51));
            SetBrush(resources, "Brush.Theme.Surface", Color.FromRgb(28, 29, 36));
            SetBrush(resources, "Brush.Theme.SurfaceAlt", Color.FromRgb(36, 38, 48));
            SetBrush(resources, "Brush.Theme.Border", Color.FromRgb(46, 49, 61));
            SetBrush(resources, "Brush.Theme.CardBackground", Color.FromRgb(24, 25, 31));
            SetBrush(resources, "Brush.Theme.CardBorder", Color.FromRgb(42, 45, 58));
            SetBrush(resources, "Brush.Theme.CardHover", Color.FromRgb(32, 34, 43));
            SetBrush(resources, "Brush.Theme.TextPrimary", Color.FromRgb(249, 250, 251));
            SetBrush(resources, "Brush.Theme.TextSecondary", Color.FromRgb(156, 163, 175));
            SetBrush(resources, "Brush.Theme.TextMuted", Color.FromRgb(107, 114, 128));
            SetBrush(resources, "Brush.Theme.InputBackground", Color.FromRgb(30, 32, 41));
            SetBrush(resources, "Brush.Theme.InputBorder", Color.FromRgb(58, 62, 77));
            SetBrush(resources, "Brush.Theme.InputForeground", Color.FromRgb(249, 250, 251));

            SetBrush(resources, "Brush.Theme.Accent", accentRed);
            SetBrush(resources, "Brush.Theme.AccentHover", Color.FromRgb(255, 43, 56));
            SetBrush(resources, "Brush.Theme.AccentPressed", Color.FromRgb(166, 4, 13));
            SetBrush(resources, "Brush.Theme.AccentForeground", white);

            SetBrush(resources, "Brush.Theme.ButtonSecondaryBackground", Color.FromRgb(36, 38, 48));
            SetBrush(resources, "Brush.Theme.ButtonSecondaryBorder", Color.FromRgb(55, 59, 74));
            SetBrush(resources, "Brush.Theme.ButtonSecondaryForeground", Color.FromRgb(229, 231, 235));

            SetBrush(resources, "Brush.Theme.BadgeOnlineBackground", Color.FromRgb(6, 78, 59));
            SetBrush(resources, "Brush.Theme.BadgeOnlineForeground", Color.FromRgb(110, 231, 183));
            SetBrush(resources, "Brush.Theme.BadgeOfflineBackground", Color.FromRgb(55, 65, 81));
            SetBrush(resources, "Brush.Theme.BadgeOfflineForeground", Color.FromRgb(156, 163, 175));
            SetBrush(resources, "Brush.Theme.Error", Color.FromRgb(248, 113, 113));
            SetBrush(resources, "Brush.Theme.Success", Color.FromRgb(52, 211, 153));
        }
        else
        {
            // Light Theme - Crisp Clean White & Vibrant BeRDP Red
            SetBrush(resources, "Brush.Theme.Background", Color.FromRgb(248, 249, 250));
            SetBrush(resources, "Brush.Theme.TopBarBackground", white);
            SetBrush(resources, "Brush.Theme.TopBarBorder", Color.FromRgb(229, 231, 235));
            SetBrush(resources, "Brush.Theme.Surface", white);
            SetBrush(resources, "Brush.Theme.SurfaceAlt", Color.FromRgb(243, 244, 246));
            SetBrush(resources, "Brush.Theme.Border", Color.FromRgb(229, 231, 235));
            SetBrush(resources, "Brush.Theme.CardBackground", white);
            SetBrush(resources, "Brush.Theme.CardBorder", Color.FromRgb(229, 231, 235));
            SetBrush(resources, "Brush.Theme.CardHover", Color.FromRgb(249, 250, 251));
            SetBrush(resources, "Brush.Theme.TextPrimary", Color.FromRgb(17, 24, 39));
            SetBrush(resources, "Brush.Theme.TextSecondary", Color.FromRgb(75, 85, 99));
            SetBrush(resources, "Brush.Theme.TextMuted", Color.FromRgb(156, 163, 175));
            SetBrush(resources, "Brush.Theme.InputBackground", white);
            SetBrush(resources, "Brush.Theme.InputBorder", Color.FromRgb(209, 213, 219));
            SetBrush(resources, "Brush.Theme.InputForeground", Color.FromRgb(17, 24, 39));

            SetBrush(resources, "Brush.Theme.Accent", accentRed);
            SetBrush(resources, "Brush.Theme.AccentHover", Color.FromRgb(201, 5, 16));
            SetBrush(resources, "Brush.Theme.AccentPressed", Color.FromRgb(166, 4, 13));
            SetBrush(resources, "Brush.Theme.AccentForeground", white);

            SetBrush(resources, "Brush.Theme.ButtonSecondaryBackground", Color.FromRgb(243, 244, 246));
            SetBrush(resources, "Brush.Theme.ButtonSecondaryBorder", Color.FromRgb(209, 213, 219));
            SetBrush(resources, "Brush.Theme.ButtonSecondaryForeground", Color.FromRgb(55, 65, 81));

            SetBrush(resources, "Brush.Theme.BadgeOnlineBackground", Color.FromRgb(222, 247, 236));
            SetBrush(resources, "Brush.Theme.BadgeOnlineForeground", Color.FromRgb(3, 84, 63));
            SetBrush(resources, "Brush.Theme.BadgeOfflineBackground", Color.FromRgb(243, 244, 246));
            SetBrush(resources, "Brush.Theme.BadgeOfflineForeground", Color.FromRgb(107, 114, 128));
            SetBrush(resources, "Brush.Theme.Error", Color.FromRgb(220, 38, 38));
            SetBrush(resources, "Brush.Theme.Success", Color.FromRgb(16, 185, 129));
        }

        if (persist)
        {
            SaveTheme(theme);
        }

        ThemeChanged?.Invoke();
    }

    private static void SetBrush(ResourceDictionary resources, string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        resources[key] = brush;
    }

    private static AppTheme LoadSavedTheme()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var content = File.ReadAllText(SettingsPath).Trim();
                if (Enum.TryParse<AppTheme>(content, true, out var result))
                {
                    return result;
                }
            }
        }
        catch { }
        return AppTheme.Light; // Default modern BeRDP Light theme
    }

    private static void SaveTheme(AppTheme theme)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(SettingsPath, theme.ToString());
        }
        catch { }
    }
}
