using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using System.Threading.Tasks;

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

    [ObservableProperty]
    private ObservableCollection<InventoryItem> _allInventoryItems = new();

    [ObservableProperty]
    private string _appVersion = "1.0.0";

    private List<InventoryItem> _fullInventoryList = new();
    private List<StockTransactionLog> _fullTransactionLogs = new();

    private string _searchQuery = string.Empty;
    public string SearchQuery {
        get => _searchQuery;
        set {
            if (SetProperty(ref _searchQuery, value)) {
                FilterData();
            }
        }
    }

    public MainViewModel() {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        string ? path = assembly.Location;

        if (!string.IsNullOrEmpty(path)) {
            AppVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(path).ProductVersion ?? "1.0.0";
        } else {
            AppVersion = "1.0.0"; // Fallback for environments where location is unavailable
        }
        LoadData();
    }

    [RelayCommand]
    private void LoadData() {
        // 1. Fetch Inventory
        var items = _repository.GetAllItems();
        foreach(var item in items) {
            item.Final_Stock = item.InitialStock + item.Stock_In - item.Stock_Out;
        }
        _fullInventoryList = items; 
        AllInventoryItems = new ObservableCollection<InventoryItem>(items); 
        
        // 2. Fetch Transaction Logs
        var logs = _repository.GetTransactionLogs();
        _fullTransactionLogs = logs; // Save the raw logs

        // 3. Apply active filters to BOTH lists to update the UI
        FilterData(); 
    }

    private void FilterData() {
        if (string.IsNullOrWhiteSpace(SearchQuery)) {
            InventoryList = new ObservableCollection<InventoryItem>(_fullInventoryList);
            TransactionLogs = new ObservableCollection<StockTransactionLog>(_fullTransactionLogs);
            return;
        }

        var query = SearchQuery.ToLower();

        // Filter Inventory
        var filteredInventory = _fullInventoryList.Where(item => 
            (item.ItemCode?.ToLower().Contains(query) == true) ||
            (item.Description?.ToLower().Contains(query) == true) ||
            (item.ManufacturerSupplier?.ToLower().Contains(query) == true) ||
            (item.Location?.ToLower().Contains(query) == true) ||
            (item.Remarks?.ToLower().Contains(query) == true)
        ).ToList();
        
        InventoryList = new ObservableCollection<InventoryItem>(filteredInventory);

        // Filter Transaction Logs
        var filteredLogs = _fullTransactionLogs.Where(log => 
            (log.ItemCode?.ToLower().Contains(query) == true) ||
            (log.ItemDescription?.ToLower().Contains(query) == true) ||
            (log.NameRequested?.ToLower().Contains(query) == true) ||
            (log.TransactionType?.ToLower().Contains(query) == true) ||
            (log.Date?.ToLower().Contains(query) == true) ||
            (log.Remarks?.ToLower().Contains(query) == true)
        ).ToList();

        TransactionLogs = new ObservableCollection<StockTransactionLog>(filteredLogs);
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
                Stock_In = 0,
                Stock_Out = 0,
                Final_Stock = 0,
                Location = string.Empty,
                Remarks = string.Empty,
                Status = string.Empty
        };

        IsAddDialogVisible = true;
    }

    [RelayCommand]
    private void CloseAddDialog() {
        IsAddDialogVisible = false;
    }

    [RelayCommand]
    private void SaveNewItem() {
        try {
            _repository.AddItem(NewItemForm);
            LoadData();
            IsAddDialogVisible = false;
            ShowNotification("Item successfully added to inventory."); // Success Notification
        } catch {
            ShowNotification("Error: Could not add item.", true); // Error Notification
        }
    }

    [RelayCommand]
    private void DeleteSelectedItems(System.Collections.IList selectedItems) {
        if (selectedItems == null || selectedItems.Count == 0) return;

        var itemsToDelete = selectedItems.Cast<InventoryItem>().ToList();
        foreach(var item in itemsToDelete) {
            _repository.DeleteItem(item.ItemCode);
        }
        LoadData();
        ShowNotification($"{itemsToDelete.Count} item(s) deleted.");
    }

    [RelayCommand]
    private void SaveStockOut() {
        try {
            // Item Validation Logic
            var currentItem = _fullInventoryList.FirstOrDefault(i => i.ItemCode == StockOutForm.ItemCode);
            if (currentItem != null && StockOutForm.Quantity > currentItem.Final_Stock) {
                // Show the red error notification
                ShowNotification($"Insufficient stock! Only {currentItem.Final_Stock} available.", true);
                // Use 'return' to immediately stop the method so it DOES NOT save to the database
                return; 
            }
            // If the quantity is valid, proceed with saving
            _repository.ProcessTransaction(StockOutForm);
            LoadData(); 
            IsStockOutDialogVisible = false; 
            ShowNotification("Stock out request logged successfully."); 
        } catch {
            ShowNotification("Error: Failed to log stock out.", true); 
        }
    }

    [RelayCommand]
    private void SaveStockIn() {
        try {
            _repository.ProcessTransaction(StockInForm);
            LoadData(); 
            IsStockInDialogVisible = false; // Close the dialog
            ShowNotification("Delivery (Stock In) added successfully."); 
        } catch {
            ShowNotification("Error: Failed to add delivery.", true); 
        }
    }

    [RelayCommand]
    private void DeleteSelectedLogs(System.Collections.IList selectedLogs) {
        if (selectedLogs == null || selectedLogs.Count == 0) return;
        
        var logsToDelete = selectedLogs.Cast<StockTransactionLog>().ToList();
        foreach(var log in logsToDelete) {
            _repository.DeleteTransactionLog(log.TransactionId);
        }
        LoadData(); 
        ShowNotification($"{logsToDelete.Count} log(s) deleted.");
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

    public void UpdateItemInDatabase(InventoryItem item) {
        _repository.UpdateItem(item);
    }

    public void UpdateTransactionLogInDatabase(StockTransactionLog log) {
        _repository.UpdateTransactionLog(log);
        LoadData();
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


    [ObservableProperty]
    private string _snackbarMessage = string.Empty;

    [ObservableProperty]
    private string _snackbarColor = "#323232"; // Default Dark Gray for success/info

    [ObservableProperty]
    private double _snackbarOpacity = 0.0;

    private async void ShowNotification(string message, bool isError = false) {
        SnackbarMessage = message;
        SnackbarColor = isError ? "#D93025" : "#323232";
        SnackbarOpacity = 1.0;

        await Task.Delay(3000);

        if (SnackbarMessage == message) {
            SnackbarOpacity = 0.0; 
        }
    }
}