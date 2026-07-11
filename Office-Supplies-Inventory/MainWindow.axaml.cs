using Avalonia.Controls;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Interactivity;
using System.IO;
using System.Text.Json;
using NetSparkleUpdater;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.SignatureVerifiers;

namespace Office_Supplies_Inventory;

public partial class MainWindow: Window {
    private readonly string _settingsPath = "settings.json";
    private AppSettings _currentSettings = new(); // Holds all active settings

    private SparkleUpdater _sparkle;
    public MainWindow() {
        InitializeComponent();
        DataContext = new MainViewModel();
        LoadSettings();

        string appcastUrl = "https://github.com/Cadlaxa/Office-Inventory-System/releases/latest/download/appcast.xml";
        
        _sparkle = new SparkleUpdater(appcastUrl, new Ed25519Checker(SecurityMode.Unsafe)) {
            UIFactory = new NetSparkleUpdater.UI.Avalonia.UIFactory(
                Icon
            )
        };
        _sparkle.StartLoop(true, true);
    }

    private void LoadSettings() {
        try {
            if (File.Exists(_settingsPath)) {
                string json = File.ReadAllText(_settingsPath);
                
                // Deserialize the entire JSON object into our AppSettings class
                var savedSettings = JsonSerializer.Deserialize<AppSettings>(json);
                if (savedSettings != null) {
                    _currentSettings = savedSettings;
                }
            }
        } 
        catch { 
            // If it fails (e.g., corrupted file), _currentSettings remains at its defaults
        }
        ThemeToggle.IsChecked = _currentSettings.IsDarkMode;
        ApplyTheme(_currentSettings.IsDarkMode);
    }

    private void SaveSettings() {
        try {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(_currentSettings, options);
            File.WriteAllText(_settingsPath, json);
        } 
        catch { 
            // Ignore write errors
        }
    }

    private void InventoryGrid_CellEditEnded(object ? sender, DataGridCellEditEndedEventArgs e) {
        if (e.EditAction == DataGridEditAction.Commit) {
            var editedItem = e.Row.DataContext as InventoryItem;
            if (editedItem != null && DataContext is MainViewModel vm) {
                vm.UpdateItemInDatabase(editedItem);
            }
        }
    }

    private void TransactionLogGrid_CellEditEnded(object ? sender, DataGridCellEditEndedEventArgs e) {
        if (e.EditAction == DataGridEditAction.Commit) {
            var editedLog = e.Row.DataContext as StockTransactionLog;
            if (editedLog != null && DataContext is MainViewModel vm) {
                vm.UpdateTransactionLogInDatabase(editedLog);
            }
        }
    }

    private void ThemeToggle_Toggled(object? sender, RoutedEventArgs e) {
        if (ThemeToggle != null) {
            bool isDark = ThemeToggle.IsChecked == true;
            _currentSettings.IsDarkMode = isDark;
            ApplyTheme(isDark);
            SaveSettings();
        }
    }

    private void ApplyTheme(bool isDark) {
        if (Application.Current != null) {
            Application.Current.RequestedThemeVariant = isDark ? 
                ThemeVariant.Dark : 
                ThemeVariant.Light;
        }
    }

    public class AppSettings {
        public bool IsDarkMode {get; set;} = false;
    }
}