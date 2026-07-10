using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;

namespace Office_Supplies_Inventory;

public partial class MainViewModel: ObservableObject {
    private readonly InventoryRepository _repository = new();
    [ObservableProperty]
    private ObservableCollection < InventoryItem > _inventoryList;

    [ObservableProperty]
    private InventoryItem _selectedItem;
    [ObservableProperty]
    private ObservableCollection<StockTransactionLog> _transactionLogs;

    [ObservableProperty]
    private bool _isAddDialogVisible;

    [ObservableProperty]
    private InventoryItem _newItemForm = new();
    public ObservableCollection<InventoryItem> AllInventoryItems => InventoryList;

    [ObservableProperty]
    private string _appVersion = "1.0.0";

    public MainViewModel() {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        AppVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion ?? "1.0.0";
        LoadData();
    }

    [RelayCommand]
    private void LoadData() {
        var items = _repository.GetAllItems();

        foreach(var item in items) {
            item.FinalStock = item.InitialStock + item.StockIn - item.StockOut;
        }
        InventoryList = new ObservableCollection < InventoryItem > (items);

        // Fetch Transaction Logs
        var logs = _repository.GetTransactionLogs();
        TransactionLogs = new ObservableCollection < StockTransactionLog > (logs);
    }

    [RelayCommand]
    private void OpenAddDialog() {
        // Prep a completely blank form for manual entry
        NewItemForm = new InventoryItem {
            ItemCode = string.Empty,
                Description = string.Empty,
                ManufacturerSupplier = string.Empty,
                AsOfDate = System.DateTime.Now.ToString("dd-MMM-yy"),
                InitialStock = 0,
                StockIn = 0,
                StockOut = 0,
                FinalStock = 0,
                Location = string.Empty,
                Remarks = string.Empty
        };

        IsAddDialogVisible = true;
    }

    [RelayCommand]
    private void CloseAddDialog() {
        IsAddDialogVisible = false;
    }

    [RelayCommand]
    private void SaveNewItem() {
        // Save the typed data to the database
        _repository.AddItem(NewItemForm);

        // Refresh the background grid and close the popup
        LoadData();
        IsAddDialogVisible = false;
    }

    // --- EXISTING COMMANDS ---
    [RelayCommand]
    private void DeleteSelectedItems(System.Collections.IList selectedItems) {
        // Check if nothing is selected
        if (selectedItems == null || selectedItems.Count == 0) return;

        // Convert the selected items into a safe list we can loop through
        var itemsToDelete = selectedItems.Cast < InventoryItem > ().ToList();

        foreach(var item in itemsToDelete) {
            _repository.DeleteItem(item.ItemCode);
        }

        LoadData();
    }

    [RelayCommand]
    private void StockOut() {
        if (SelectedItem == null) return;

        // Simulate reading from a form where the admin types the employee's name and quantity
        var log = new StockTransactionLog {
            ItemCode = SelectedItem.ItemCode,
                TransactionType = "OUT",
                Quantity = 1,
                NameRequested = "Employee Name",
                Date = System.DateTime.Now.ToString("dd-MMM-yy")
        };

        _repository.ProcessTransaction(log);
        LoadData();
    }

    [RelayCommand]
    private void DeleteSelectedLogs(System.Collections.IList selectedLogs) {
        if (selectedLogs == null || selectedLogs.Count == 0) return;
        
        var logsToDelete = selectedLogs.Cast<StockTransactionLog>().ToList();
        foreach(var log in logsToDelete) {
            _repository.DeleteTransactionLog(log.TransactionId);
        }
        LoadData(); 
    }

    // STOCK OUT FORM
    [ObservableProperty]
    private bool _isStockOutDialogVisible;

    [ObservableProperty]
    private StockTransactionLog _stockOutForm = new();

    [RelayCommand]
    private void OpenStockOutDialog() {
        if (SelectedItem == null) return; // Prevent opening if nothing is selected

        StockOutForm = new StockTransactionLog {
            ItemCode = SelectedItem.ItemCode,
                NameRequested = string.Empty,
                ItemDescription = SelectedItem.Description,
                Date = System.DateTime.Now.ToString("dd-MMM-yy"),
                TransactionType = "OUT",
                Quantity = 1,
                Remarks = string.Empty
        };
        IsStockOutDialogVisible = true;
    }

    [RelayCommand]
    private void CloseStockOutDialog() => IsStockOutDialogVisible = false;

    [RelayCommand]
    private void SaveStockOut() {
        _repository.ProcessTransaction(StockOutForm);
        LoadData(); // Refresh both grids
        IsStockOutDialogVisible = false;
    }

    public void UpdateItemInDatabase(InventoryItem item) {
        _repository.UpdateItem(item);
    }

    public void UpdateTransactionLogInDatabase(StockTransactionLog log) {
        _repository.UpdateTransactionLog(log);
    }

    [ObservableProperty]
    private bool _isStockInDialogVisible;

    [ObservableProperty]
    private StockTransactionLog _stockInForm = new();

    [RelayCommand]
    private void OpenStockInDialog() {
        if (SelectedItem == null) return;

        StockInForm = new StockTransactionLog {
            ItemCode = SelectedItem.ItemCode,
                ItemDescription = SelectedItem.Description,
                Date = System.DateTime.Now.ToString("dd-MMM-yy"),
                TransactionType = "IN",
                Quantity = 1
        };
        IsStockInDialogVisible = true;
    }

    [RelayCommand]
    private void CloseStockInDialog() => IsStockInDialogVisible = false;

    [RelayCommand]
    private void SaveStockIn() {
        _repository.ProcessTransaction(StockInForm);
        LoadData();
        IsStockInDialogVisible = false;
    }
}