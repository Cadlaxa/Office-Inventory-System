using Avalonia.Controls;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Interactivity;
namespace Office_Supplies_Inventory;

public partial class MainWindow: Window {
    public MainWindow() {
        InitializeComponent();
        DataContext = new MainViewModel();
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

    private void ThemeToggle_Toggled(object ? sender, RoutedEventArgs e) {
        if (sender is ToggleSwitch toggle && Application.Current != null) {
            Application.Current.RequestedThemeVariant = toggle.IsChecked == true ?
                ThemeVariant.Dark :
                ThemeVariant.Light;
        }
    }
}