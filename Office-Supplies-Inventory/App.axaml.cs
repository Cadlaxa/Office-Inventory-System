using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace Office_Supplies_Inventory;

public partial class App : Application {
    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted() {
        /*var mgr = new UpdateManager(new GithubSource("https://github.com/Cadlaxa/Office-Inventory-System", null, false));
        var newVersion = await mgr.CheckForUpdatesAsync();

        if (newVersion != null) {
            var result = await ShowUpdateConfirmationDialog(newVersion.TargetFullRelease.Version.ToString());
            if (result) {
                await mgr.DownloadUpdatesAsync(newVersion);
                mgr.ApplyUpdatesAndRestart(newVersion);
            }
        }*/

        base.OnFrameworkInitializationCompleted();
    }

    private async Task<bool> ShowUpdateConfirmationDialog(string version) {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            var window = desktop.MainWindow;
            var resultSource = new TaskCompletionSource<bool>();

            var updateButton = new Button {
                Content = "Update",
                IsDefault = true,
                Width = 80,
                Margin = new Thickness(0, 0, 10, 0)
            };
            var cancelButton = new Button {
                Content = "Cancel",
                IsCancel = true,
                Width = 80
            };

            var dialog = new Window {
                Title = "Update available",
                Width = 400,
                Height = 150,
                CanResize = false,
                Content = new StackPanel {
                    Margin = new Thickness(10),
                    Children = {
                        new TextBlock {
                            Text = $"A new version ({version}) is available. Do you want to update now?",
                        },
                        new StackPanel {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Margin = new Thickness(0, 10, 0, 0),
                            Children = { updateButton, cancelButton }
                        }
                    }
                }
            };

            updateButton.Click += (_, _) => {
                resultSource.TrySetResult(true);
                dialog.Close();
            };
            cancelButton.Click += (_, _) => {
                resultSource.TrySetResult(false);
                dialog.Close();
            };

            dialog.Closed += (_, _) => resultSource.TrySetResult(false);
            if (window != null) {
                dialog.Icon = window.Icon;
                await dialog.ShowDialog(window);
            } else {
                await dialog.ShowDialog((Window?)null);
            }

            return await resultSource.Task;
        }

        return false;
    }

    public void SetTheme(ThemeVariant theme) {
        Application.Current!.RequestedThemeVariant = theme;
    }
}
