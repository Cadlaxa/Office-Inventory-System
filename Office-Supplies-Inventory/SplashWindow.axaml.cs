using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;

namespace Office_Supplies_Inventory {
    public partial class SplashWindow: Window {
        public SplashWindow() {
            InitializeComponent();

            this.Cursor = new Cursor(StandardCursorType.AppStarting);
            this.Opened += SplashWindow_Opened;
        }
        private async void SplashWindow_Opened(object ? sender, EventArgs e) {
            this.Opened -= SplashWindow_Opened;
            var viewModel = new MainViewModel();
            await viewModel.InitializeDataAsync();
            await Task.Delay(1000);
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                var mainWindow = new MainWindow {
                    DataContext = viewModel
                };
                mainWindow.Show();
                desktop.MainWindow = mainWindow;
                this.Close();
            }
        }
    }
}