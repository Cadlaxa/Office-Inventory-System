using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using System.IO;
using System.Text.Json;

namespace Office_Supplies_Inventory;

public partial class App: Application {
    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted() {
        ApplySavedTheme();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            desktop.MainWindow = new SplashWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ApplySavedTheme() {
        try {
            string settingsFilePath = "settings.json";

            if (File.Exists(settingsFilePath)) {
                string json = File.ReadAllText(settingsFilePath);
                if (json.Contains("\"IsDarkMode\": true") || json.Contains("\"Theme\": \"Dark\"")) {
                    Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
                } else {
                    Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
                }
            }
        } catch {
        }
    }

    public void SetTheme(ThemeVariant theme) {
        Application.Current!.RequestedThemeVariant = theme;
    }
}