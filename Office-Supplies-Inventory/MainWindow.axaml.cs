using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Text.Json;
using NetSparkleUpdater.Interfaces;
using NetSparkleUpdater;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.SignatureVerifiers;
using System.Runtime.InteropServices;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using System.ComponentModel;

namespace Office_Supplies_Inventory;

public partial class MainWindow: Window {
    public readonly string _settingsPath = "settings.json";
    private AppSettings _currentSettings = new(); // Holds all active settings
    private SparkleUpdater _sparkle;
    public MainWindow() {

        InitializeComponent();
        LoadSettings();
        AddHandler(DragDrop.DragEnterEvent, Window_DragEnter);
        AddHandler(DragDrop.DragLeaveEvent, Window_DragLeave);
        AddHandler(DragDrop.DragOverEvent, Window_DragOver);
        AddHandler(DragDrop.DropEvent, Window_Drop);
        var viewModel = new MainViewModel();
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        DataContext = viewModel;
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        this.Title = $"Office Supplies Inventory System v{version.Major}.{version.Minor}.{version.Build}";
        string appcastUrl = "https://github.com/Cadlaxa/Office-Inventory-System/releases/latest/download/appcast.xml";
        
        if (!Design.IsDesignMode) {
            // Dynamically set the executable name based on the OS
            string executableName = "Office-Supplies-Inventory.exe";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                // macOS MUST target the .app bundle wrapper to restart correctly
                executableName = "Office-Supplies-Inventory.app"; 
            } 
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                // Linux continues to use the raw binary with no extension
                executableName = "Office-Supplies-Inventory"; 
            }

            _sparkle = new SparkleUpdater(appcastUrl, new Ed25519Checker(SecurityMode.Unsafe)) {
                UIFactory = new NetSparkleUpdater.UI.Avalonia.UIFactory(Icon),
                RestartExecutableName = executableName,
                LogWriter = new FileLogger()
            };

            _sparkle.CloseApplication += () => {
                // Forcefully and immediately kill the app to drop all file and database locks
                System.Environment.Exit(0);
            };

            _sparkle.DownloadFinished += (item, path) => {
                
                // We only need to do this custom unzip bypass on Windows.
                // Mac and Linux handle their .tar.gz files natively without issue!
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    
                    // GitHub strips the extension. We must rename the file to end in .zip so PowerShell can read it.
                    string zipPath = path + ".zip";
                    if (File.Exists(zipPath)) File.Delete(zipPath);
                    File.Move(path, zipPath);

                    // Set up our paths
                    string appDir = AppDomain.CurrentDomain.BaseDirectory;
                    string scriptPath = Path.Combine(Path.GetTempPath(), "netsparkle_powershell_update.cmd");
                    string exeName = "Office-Supplies-Inventory.exe";
                    string exePath = Path.Combine(appDir, exeName);

                    // Write a custom batch script that uses PowerShell to cleanly extract the files
                    string script = $@"@echo off
    echo Installing Office Supplies Inventory Update... Please wait.
    timeout /t 2 /nobreak > nul
    powershell.exe -Command ""Expand-Archive -Path '{zipPath}' -DestinationPath '{appDir}' -Force""
    start """" ""{exePath}""
    del ""{zipPath}""
    del ""%~f0""
    ";
                    File.WriteAllText(scriptPath, script);

                    Process.Start(new ProcessStartInfo {
                        FileName = scriptPath,
                        UseShellExecute = true,
                        CreateNoWindow = true
                    });
                    System.Environment.Exit(0);
                }
            };
            
            _sparkle.StartLoop(true, true);
        }
    }
    
    private void Window_DragEnter(object? sender, DragEventArgs e) {
        if (e.Data.Contains(Avalonia.Input.DataFormats.Files)) {
            if (!DragDropOverlay.IsVisible) {
                DragDropOverlay.IsVisible = true;
                DragDropOverlay.Classes.Add("show"); 
            }
        }
    }

    private void Window_DragLeave(object? sender, DragEventArgs e) {
        var pos = e.GetPosition(this);
        // ONLY hide the overlay if the mouse cursor physically crosses outside the edges of the window
        if (pos.X <= 0 || pos.Y <= 0 || pos.X >= this.Bounds.Width || pos.Y >= this.Bounds.Height) {
            DragDropOverlay.Classes.Remove("show");
            DragDropOverlay.IsVisible = false;
        }
    }

    private void Window_DragOver(object? sender, DragEventArgs e) {
        if (e.Data.Contains(Avalonia.Input.DataFormats.Files)) {
            e.DragEffects = DragDropEffects.Copy;
        } else {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void Window_Drop(object? sender, DragEventArgs e) {
        DragDropOverlay.Classes.Remove("show");
        DragDropOverlay.IsVisible = false;

        if (e.Data.Contains(Avalonia.Input.DataFormats.Files)) {
            var files = e.Data.GetFiles();
            var firstFile = files?.FirstOrDefault();
            
            if (firstFile != null) {
                string filePath = firstFile.Path.LocalPath ?? string.Empty;

                if (filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || 
                    filePath.EndsWith(".xlsm", StringComparison.OrdinalIgnoreCase)) {
                    
                    if (DataContext is MainViewModel vm) {
                        await vm.ImportDataFromFileAsync(filePath);
                    }
                } else {
                    if (DataContext is MainViewModel vm) {
                        vm.ShowNotification("Invalid file! Please drop an .xlsx or .xlsm file.", true); 
                    }
                }
            }
        }
    }

    protected override void OnDataContextChanged(EventArgs e) {
        base.OnDataContextChanged(e);
        if (DataContext is MainViewModel vm) {
            vm.IsSidebarExpanded = _currentSettings.IsSidebarExpanded;
            vm.PropertyChanged -= ViewModel_PropertyChanged; 
            vm.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object ? sender, PropertyChangedEventArgs e) {
        if (sender is MainViewModel vm) {
            if (e.PropertyName == nameof(vm.IsSidebarExpanded)) {
                _currentSettings.IsSidebarExpanded = vm.IsSidebarExpanded;
                SaveSettings();
            }

            // 1. Focus Add Dialog
            if (e.PropertyName == nameof(vm.IsAddDialogVisible) && vm.IsAddDialogVisible) {
                ForceFocus(AddItemCodeTextBox);
            }
            // 2. Focus Edit Dialog
            else if (e.PropertyName == nameof(vm.IsEditDialogVisible) && vm.IsEditDialogVisible) {
                ForceFocus(EditItemDescTextBox);
            }
            // 3. Focus Stock Out Dialog
            else if (e.PropertyName == nameof(vm.IsStockOutDialogVisible) && vm.IsStockOutDialogVisible) {
                ForceFocus(StockOutComboBox);
            }
            // 4. Focus Stock In Dialog
            else if (e.PropertyName == nameof(vm.IsStockInDialogVisible) && vm.IsStockInDialogVisible) {
                ForceFocus(StockInComboBox);
            }
        }
    }
    private async void ForceFocus(Control targetControl) {
        await Task.Delay(400);
        Dispatcher.UIThread.Post(() => {
            targetControl.Focus();
            var topLevel = TopLevel.GetTopLevel(targetControl);
            if (targetControl is TextBox tb) {
                tb.CaretIndex = tb.Text?.Length ?? 0;
            }

        }, DispatcherPriority.Input);
    }

    private void EditEntryMenuItem_Click(object ? sender, Avalonia.Interactivity.RoutedEventArgs e) {
       if (this.DataContext is MainViewModel viewModel) {
           if (InventoryGrid.SelectedItem != null) {
               viewModel.OpenEditDialog();
           }
       }
    }

    private void GotoItemMenuItem_Click(object ? sender, Avalonia.Interactivity.RoutedEventArgs e) {
        if (this.DataContext is MainViewModel viewModel) {
            if (viewModel.SelectedLog != null && !string.IsNullOrEmpty(viewModel.SelectedLog.ItemCode)) {
                string targetCode = viewModel.SelectedLog.ItemCode;
                viewModel.SelectedTabIndex = 0;;
                ScrollToItem(targetCode);
            }
        }
    }

    private void DeselectMenuItem_Click(object ? sender, Avalonia.Interactivity.RoutedEventArgs e) {
        if (InventoryGrid.SelectedItems != null) {
            InventoryGrid.SelectedItems.Clear();
        }
        if (TransactionLogGrid.SelectedItems != null) {
            TransactionLogGrid.SelectedItems.Clear();
        }
    }

    public void LoadSettings() {
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

    private void InventoryGrid_DoubleTapped(object? sender, TappedEventArgs e) {
        if (DataContext is MainViewModel viewModel) {
            viewModel.OpenEditDialog();
            e.Handled = true; 
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
        public bool IsDarkMode { get; set; } = false;
        public bool IsSidebarExpanded { get; set; } = true; // Added property
    }

    public class FileLogger : ILogger {
        private readonly string _logPath = "logs/updater_debug_log.txt";
        
        public FileLogger() {
            File.WriteAllText(_logPath, "--- NetSparkle Updater Log Started ---\n");
        }

        public void PrintMessage(string message, params object[] arguments) {
            try {
                string formatted = arguments.Length > 0 ? string.Format(message, arguments) : message;
                File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] {formatted}\n");
            } catch { 
            }
        }
    }

    public void CheckForUpdates_Click(object sender, RoutedEventArgs e) {
        _sparkle.CheckForUpdatesAtUserRequest();
    }

    public void ScrollToItem(string itemCode) {
        Dispatcher.UIThread.InvokeAsync(() => {
            var itemsEnumerable = InventoryGrid.ItemsSource as IEnumerable;
            var itemToFocus = itemsEnumerable?.Cast<InventoryItem>()
                .FirstOrDefault(i => i.ItemCode == itemCode);

            if (itemToFocus != null) {
                InventoryGrid.SelectedItem = itemToFocus;
                InventoryGrid.ScrollIntoView(itemToFocus, null);
            }
        });
    }

    public async void ScrollToBottomOfLog() {
        if (this.DataContext is MainViewModel vm) {
            vm.SelectedTabIndex = 1; 
        }
        await Task.Delay(200); 
        var itemsEnumerable = TransactionLogGrid.ItemsSource as System.Collections.IEnumerable;
        var lastItem = itemsEnumerable?.Cast<StockTransactionLog>().LastOrDefault();
        if (lastItem != null) {
            TransactionLogGrid.SelectedItem = lastItem;
            TransactionLogGrid.ScrollIntoView(lastItem, null);
        }
    }

    private void SearchBar_GotFocus(object ? sender, Avalonia.Input.GotFocusEventArgs e) {
        // When the user clicks the search bar, instantly open the command dropdown
        if (sender is Avalonia.Controls.AutoCompleteBox searchBox) {
            Dispatcher.UIThread.InvokeAsync(() => {
                searchBox.IsDropDownOpen = true;
            });
        }
    }

    private void DataGrid_SelectionChanged(object ? sender, Avalonia.Controls.SelectionChangedEventArgs e) {
        if (sender is Avalonia.Controls.DataGrid grid && this.DataContext is MainViewModel vm) {
            vm.CurrentSelectedItems = grid.SelectedItems;
            if (grid.SelectedItem != null) {
                grid.ScrollIntoView(grid.SelectedItem, null);
            }
            int count = grid.SelectedItems.Count;

            if (count > 3) {
                vm.ShowSelectionNotification($"{count} items selected");
            } else if (count >= 1 && count <= 3) {
                var selectedCodes = new System.Collections.Generic.List < string > ();

                foreach(var selectedObj in grid.SelectedItems) {
                    if (selectedObj is InventoryItem item) {
                        selectedCodes.Add(item.ItemCode);
                    } else if (selectedObj is StockTransactionLog log) {
                        selectedCodes.Add(log.ItemCode);
                    }
                }
                string joinedCodes = string.Join(", ", selectedCodes);
                vm.ShowSelectionNotification($"Selected: {joinedCodes}");
            }
        }
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e) {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.L) {
            MainSearchBox.Focus();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape) {
            if (DataContext is MainViewModel vm) {
                if (vm.IsAddDialogVisible) {
                    vm.CloseAddDialogCommand.Execute(null);
                }
                else if (vm.IsEditDialogVisible) {
                    vm.CloseEditDialogCommand.Execute(null);
                }
                else if (vm.IsStockInDialogVisible) {
                    vm.CloseStockInDialogCommand.Execute(null);
                }
                else if (vm.IsStockOutDialogVisible) {
                    vm.CloseStockOutDialogCommand.Execute(null);
                }
                else if (vm.IsDeleteDialogVisible) {
                    vm.CloseDeleteDialogCommand.Execute(null);
                }
                else if (vm.IsDeleteLogDialogVisible) {
                    vm.CloseDeleteLogDialogCommand.Execute(null);
                }
                else if (vm.IsExportCompleteDialogVisible) {
                    vm.CloseDeleteLogDialogCommand.Execute(null);
                }
            }
        }
    }
}