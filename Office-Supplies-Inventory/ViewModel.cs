using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using System.IO;
using Serilog;

namespace Office_Supplies_Inventory;

public partial class MainViewModel : ObservableObject {
    private readonly InventoryRepository _repository = new();

    [ObservableProperty]
    private ObservableCollection<InventoryItem> _inventoryList;

    [ObservableProperty]
    private InventoryItem _selectedItem;

    [ObservableProperty]
    private ObservableCollection<StockTransactionLog> _transactionLogs;

    [ObservableProperty]
    private StockTransactionLog _selectedLog;

    // Add Dialog Properties
    [ObservableProperty]
    private bool _isAddDialogVisible;

    [ObservableProperty]
    private InventoryItem _newItemForm = new();

    // Edit Dialog Properties
    [ObservableProperty]
    private bool _isEditDialogVisible;

    [ObservableProperty]
    private InventoryItem _editItemForm = new();

    [ObservableProperty]
    private ObservableCollection<InventoryItem> _allInventoryItems = new();

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private string _appVersion = "1.0.0";

    [ObservableProperty]
    private bool _isStockOutDialogVisible;

    [ObservableProperty]
    private StockTransactionLog _stockOutForm = new();

    [ObservableProperty]
    private bool _isStockInDialogVisible;

    [ObservableProperty]
    private StockTransactionLog _stockInForm = new();

    // Snackbar Properties
    [ObservableProperty]
    private string _snackbarMessage = string.Empty;

    [ObservableProperty]
    private string _snackbarColor = "#323232"; // Default Dark Gray for success/info

    [ObservableProperty]
    private double _snackbarOpacity = 0.0;

    private List<InventoryItem> _fullInventoryList = new();
    private List<StockTransactionLog> _fullTransactionLogs = new();

    [ObservableProperty]
    private int _selectedTabIndex = 0; 
    public System.Collections.IList CurrentSelectedItems { get; set; }

    [RelayCommand]
    private void SwitchTab(string index) {
        if (int.TryParse(index, out int newIndex)) {
            SelectedTabIndex = newIndex;
        }
    }

    [ObservableProperty]
    private bool _isSidebarExpanded = true;

    [RelayCommand]
    public void ToggleSidebar() {
        IsSidebarExpanded = !IsSidebarExpanded;
    }

    private string _searchQuery = string.Empty;
    public string SearchQuery {
        get => _searchQuery;
        set {
            if (SetProperty(ref _searchQuery, value)) {
                FilterData();
            }
        }
    }

    public partial class SearchToken: ObservableObject {
        [ObservableProperty]
        private string _prefix; // e.g., "from:" or "code:"

        [ObservableProperty]
        private string _value; // e.g., "cadlaxa" or "123"
    }

    [ObservableProperty]
    private ObservableCollection < SearchToken > _activeSearchTokens = new();

    [RelayCommand]
    private void RemoveSearchToken(SearchToken token) {
        if (token != null) {
            ActiveSearchTokens.Remove(token);
            FilterData();
        }
    }

    public List<string> SearchCommands { get;} = 
        new List<string> {
            "name: ",
            "code: ",
            "desc: ",
            "type: ",
            "mfg: ",
            "asf: ",
            "instock: ",
            "in: ",
            "out: ",
            "final: ",
            "loc: ",
            "remark: ",
            "status: ",
            "date: ",
            "qty: "
    };

    [RelayCommand]
    private void CommitSearchToken() {
        var query = SearchQuery?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query)) return;

        var validPrefixes = new [] {
            "code:",
            "desc:",
            "mfg:",
            "asf:",
            "instock:",
            "in:",
            "out:",
            "final:",
            "loc:",
            "remark:",
            "status:",
            "date:",
            "name:",
            "type:",
            "qty:"
        };
        string foundPrefix = validPrefixes.FirstOrDefault(p => query.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        if (foundPrefix != null) {
            string val = query.Substring(foundPrefix.Length).Trim();
            if (!string.IsNullOrWhiteSpace(val)) {
                // Add as a specific command token
                ActiveSearchTokens.Add(new SearchToken {
                    Prefix = foundPrefix, Value = val
                });
            }
        } else {
            // Add as a normal global search token
            ActiveSearchTokens.Add(new SearchToken {
                Prefix = "search:", Value = query
            });
        }
        SearchQuery = string.Empty;
    }

    public MainViewModel() {
        AppVersion = "1.0.0";
    }

    public async Task InitializeDataAsync() {
        // Forces the SQLite data fetch safely into a background thread to prevent UI freezing
        await Task.Run(() => LoadData());
    }

    [RelayCommand]
    private void LoadData() {
        var items = _repository.GetAllItems();
        foreach(var item in items) {
            item.Final_Stock = item.InitialStock + item.Stock_In - item.Stock_Out;
        }
        _fullInventoryList = items;
        AllInventoryItems = new ObservableCollection<InventoryItem>(items);

        var logs = _repository.GetTransactionLogs();
        
        // Sort logic: 
        // - Valid dates are sorted chronologically (Oldest to Newest).
        // - Invalid or empty dates are pushed to the bottom using DateTime.MaxValue.
        _fullTransactionLogs = logs.OrderBy(log => {
            if (DateTime.TryParse(log.Date, out DateTime dt)) {
                return dt;
            }
            return DateTime.MaxValue;
        }).ToList();
        FilterData();
    }

    private void FilterData() {
        IEnumerable<InventoryItem> filteredInventory = _fullInventoryList;
        IEnumerable<StockTransactionLog> filteredLogs = _fullTransactionLogs;

        var allFilters = new List<(string Prefix, string Value)>();

        foreach(var token in ActiveSearchTokens) {
            allFilters.Add((token.Prefix.ToLower(), token.Value.ToLower()));
        }

        string liveQuery = SearchQuery?.ToLower().Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(liveQuery)) {
            var validPrefixes = new [] {
                "code:", "desc:", "mfg:", "asf:", "instock:", "in:", "out:", 
                "final:", "loc:", "remark:", "status:", "date:", "name:", "type:", "qty:"
            };
            string foundPrefix = validPrefixes.FirstOrDefault(p => liveQuery.StartsWith(p));

            if (foundPrefix != null) {
                string val = liveQuery.Substring(foundPrefix.Length).Trim();
                if (!string.IsNullOrWhiteSpace(val)) allFilters.Add((foundPrefix, val));
            } else {
                allFilters.Add(("search:", liveQuery));
            }
        }

        foreach(var filter in allFilters) {
            string prefix = filter.Prefix;
            string val = filter.Value;

            if (prefix == "search:") {
                filteredInventory = filteredInventory.Where(item =>
                    (item.ItemCode?.ToLower().Contains(val) == true) ||
                    (item.Description?.ToLower().Contains(val) == true) ||
                    (item.Location?.ToLower().Contains(val) == true) ||
                    (item.Remarks?.ToLower().Contains(val) == true));

                filteredLogs = filteredLogs.Where(log =>
                    (log.ItemCode?.ToLower().Contains(val) == true) ||
                    (log.ItemDescription?.ToLower().Contains(val) == true) ||
                    (log.NameRequested?.ToLower().Contains(val) == true) ||
                    (log.TransactionType?.ToLower().Contains(val) == true) ||
                    (log.Date?.ToLower().Contains(val) == true) ||
                    (log.Remarks?.ToLower().Contains(val) == true));
            } else {
                
                // 1. Tags that apply to BOTH tables
                if (prefix == "code:") {
                    filteredInventory = filteredInventory.Where(i => i.ItemCode?.ToLower().Contains(val) == true);
                    filteredLogs = filteredLogs.Where(l => l.ItemCode?.ToLower().Contains(val) == true);
                } else if (prefix == "desc:") {
                    filteredInventory = filteredInventory.Where(i => i.Description?.ToLower().Contains(val) == true);
                    filteredLogs = filteredLogs.Where(l => l.ItemDescription?.ToLower().Contains(val) == true);
                } else if (prefix == "remark:") {
                    filteredInventory = filteredInventory.Where(i => i.Remarks?.ToLower().Contains(val) == true);
                    filteredLogs = filteredLogs.Where(l => l.Remarks?.ToLower().Contains(val) == true);
                }
                
                // 2. INVENTORY ONLY Filters (Forces Logs list to empty out so tabs can switch)
                else if (prefix == "mfg:" || prefix == "asf:" || prefix == "instock:" || prefix == "in:" || prefix == "out:" || prefix == "final:" || prefix == "loc:" || prefix == "status:") {
                    
                    filteredLogs = Enumerable.Empty<StockTransactionLog>(); 

                    if (prefix == "asf:") filteredInventory = filteredInventory.Where(i => i.AsOfDate?.ToLower().Contains(val) == true);
                    else if (prefix == "instock:") filteredInventory = filteredInventory.Where(i => i.InitialStockUI?.ToLower().Contains(val) == true);
                    else if (prefix == "in:") filteredInventory = filteredInventory.Where(i => i.Stock_In.ToString().ToLower().Contains(val) == true);
                    else if (prefix == "out:") filteredInventory = filteredInventory.Where(i => i.Stock_Out.ToString().ToLower().Contains(val) == true);
                    else if (prefix == "final:") filteredInventory = filteredInventory.Where(i => i.Final_Stock.ToString().ToLower().Contains(val) == true);
                    else if (prefix == "loc:") filteredInventory = filteredInventory.Where(i => i.Location?.ToLower().Contains(val) == true);
                    else if (prefix == "status:") filteredInventory = filteredInventory.Where(i => i.Status?.ToLower().Contains(val) == true);
                }
                
                // 3. LOGS ONLY Filters (Forces Inventory list to empty out so tabs can switch)
                else if (prefix == "date:" || prefix == "name:" || prefix == "type:" || prefix == "qty:") {
                    
                    filteredInventory = Enumerable.Empty<InventoryItem>(); 

                    if (prefix == "date:") filteredLogs = filteredLogs.Where(l => l.Date?.ToLower().Contains(val) == true);
                    else if (prefix == "name:") filteredLogs = filteredLogs.Where(l => l.NameRequested?.ToLower().Contains(val) == true);
                    else if (prefix == "type:") filteredLogs = filteredLogs.Where(l => l.TransactionType?.ToLower().Contains(val) == true);
                    else if (prefix == "qty:") filteredLogs = filteredLogs.Where(l => l.Quantity.ToString().Contains(val));
                }
            }
        }

        var finalInvList = filteredInventory.ToList();
        var finalLogsList = filteredLogs.ToList();

        InventoryList = new ObservableCollection<InventoryItem>(finalInvList);
        TransactionLogs = new ObservableCollection<StockTransactionLog>(finalLogsList);

        if (allFilters.Count > 0) {
            bool foundInInventory = finalInvList.Count > 0;
            bool foundInLogs = finalLogsList.Count > 0;

            if (SelectedTabIndex == 0 && !foundInInventory && foundInLogs) SelectedTabIndex = 1;
            else if (SelectedTabIndex == 1 && !foundInLogs && foundInInventory) SelectedTabIndex = 0;
        } else {
            InventoryList = new ObservableCollection<InventoryItem>(_fullInventoryList);
            TransactionLogs = new ObservableCollection<StockTransactionLog>(_fullTransactionLogs);
        }
    }

    // --- ADD ITEM LOGIC ---

    [RelayCommand]
    private void OpenAddDialog() {
        NewItemForm = new InventoryItem {
            ItemCode = string.Empty,
            Description = string.Empty,
            AsOfDate = System.DateTime.Now.ToString("dd-MMM-yy HH:mm"),
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
    private void CloseAddDialog() => IsAddDialogVisible = false;
    
    public string LastAddedItemCode { get; set; }
    
    [RelayCommand]
    private void SaveNewItem() {
        // If the user leaves the Item Code blank, assign a unique temporary placeholder
        if (string.IsNullOrWhiteSpace(NewItemForm.ItemCode)) {
            NewItemForm.ItemCode = $"PENDING-{DateTime.Now:MMddHHmmss}";
        }
        bool isDuplicate = InventoryList.Any(item => 
            string.Equals(item.ItemCode, NewItemForm.ItemCode, StringComparison.OrdinalIgnoreCase));

        if (isDuplicate) {
            ShowNotification($"Error: The Item Code '{NewItemForm.ItemCode}' already exists!", true);
            return;
        }
        if (!int.TryParse(NewItemForm.InitialStockUI, out int parsedStock) || parsedStock < 0) {
            ShowNotification("Error: Initial Stock must be a valid number (e.g., 0, 10, 50).", true);
            return;
        }
        try {
            _repository.AddItem(NewItemForm);
            LastAddedItemCode = NewItemForm.ItemCode; 
            LoadData();
            IsAddDialogVisible = false;
            
            // Trigger the scroll in the View
            if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                if (desktop.MainWindow is MainWindow mainWindow) {
                    mainWindow.ScrollToItem(LastAddedItemCode);
                }
            }

            ShowNotification("Item successfully added to inventory.");
        } catch {
            ShowNotification("Error: Could not add item.", true);
        }
    }

    // --- EDIT ITEM LOGIC ---

    public void OpenEditDialog() {
        if (SelectedItem == null) return; 

        // Copy the data into the temporary Edit form to protect the original data
        EditItemForm = new InventoryItem {
            ItemCode = SelectedItem.ItemCode,
            Description = SelectedItem.Description,
            InitialStockUI = SelectedItem.InitialStock.ToString(),
            Location = SelectedItem.Location,
            AsOfDate = SelectedItem.AsOfDate,
            Status = SelectedItem.Status,
            Remarks = SelectedItem.Remarks,
            
            // Read-only values for the banner
            Stock_In = SelectedItem.Stock_In,
            Stock_Out = SelectedItem.Stock_Out,
            Final_Stock = SelectedItem.Final_Stock
        };

        IsEditDialogVisible = true;
    }

    [RelayCommand]
    public void ReorderItems(Tuple<int, int> indexes) {
        int oldIndex = indexes.Item1;
        int newIndex = indexes.Item2;

        if (oldIndex < 0 || newIndex < 0 || oldIndex >= InventoryList.Count || newIndex >= InventoryList.Count || oldIndex == newIndex)
            return;

        // Move the item in the UI collection
        var itemToMove = InventoryList[oldIndex];
        InventoryList.RemoveAt(oldIndex);
        InventoryList.Insert(newIndex, itemToMove);

        // Update the backend DisplayOrder values for the whole list
        for (int i = 0; i < InventoryList.Count; i++) {
            InventoryList[i].DisplayOrder = i;
        }

        // Save the new order to the database asynchronously
        Task.Run(() => {
            _repository.UpdateItemOrders(InventoryList.ToList());
        });
    }

    [RelayCommand]
    private void CloseEditDialog() => IsEditDialogVisible = false;

    [RelayCommand]
    private void SaveEditedItem() {
        if (string.IsNullOrWhiteSpace(EditItemForm.ItemCode)) {
            ShowNotification("Error: Item Code is required!", true);
            return;
        }

        try {
            _repository.UpdateItem(EditItemForm);
            LoadData();
            IsEditDialogVisible = false;
            ShowNotification("Item successfully updated.");
        } catch {
            ShowNotification("Error: Could not update item.", true);
        }
    }

    // --- DELETE ITEM LOGIC ---

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

    [ObservableProperty]
    private bool _isDeleteDialogVisible;

    private System.Collections.IList _itemsToDeletePending;

    [RelayCommand]
    private void OpenDeleteDialog(System.Collections.IList selectedItems) {
        if (selectedItems == null || selectedItems.Count == 0) return;

        _itemsToDeletePending = selectedItems;
        IsDeleteDialogVisible = true;
    }

    [RelayCommand]
    private void CloseDeleteDialog() => IsDeleteDialogVisible = false;

    [RelayCommand]
    private void ConfirmDelete() {
        if (_itemsToDeletePending == null) return;

        var itemsToDelete = _itemsToDeletePending.Cast < InventoryItem > ().ToList();
        foreach(var item in itemsToDelete) {
            _repository.DeleteItem(item.ItemCode);
        }

        IsDeleteDialogVisible = false;
        LoadData();
        ShowNotification($"{itemsToDelete.Count} item(s) deleted.");
    }

    // --- STOCK OUT LOGIC ---

    [RelayCommand]
    private async Task OpenStockOutDialog() {
        if (SelectedItem != null && SelectedItem.ItemCode.StartsWith("PENDING-")) {
            ShowNotification("Error: Please assign a valid Item Code before logging request stock out.", true);
            await Task.Delay(800);
            OpenEditDialog();
            return;
        }
        if (SelectedItem != null && SelectedItem.Final_Stock <= 0) {
            ShowNotification($"Cannot stock out. {SelectedItem.ItemCode} is completely out of stock.", true);
            return; 
        }
        StockOutForm = new StockTransactionLog {
            ItemCode = SelectedItem?.ItemCode ?? string.Empty,
            NameRequested = string.Empty,
            ItemDescription = SelectedItem?.Description ?? string.Empty,
            Date = System.DateTime.Now.ToString("dd-MMM-yy HH:mm"),
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
        if (string.IsNullOrWhiteSpace(StockOutForm.ItemCode)) {
            ShowNotification("Error: Please select an item code!", true);
            return;
        }
        if (string.IsNullOrWhiteSpace(StockOutForm.NameRequested)) {
            ShowNotification("Error: Name of Requester is required!", true);
            return;
        }
        if (!int.TryParse(StockOutForm.QuantityUI, out int parsedQuantity) || parsedQuantity < 0) {
            ShowNotification("Error: Stock Quantity must be a valid number (e.g., 0, 10, 50).", true);
            return;
        }
        if (!int.TryParse(StockOutForm.QuantityUI, out int parsedQuantity1) || parsedQuantity1 == 0 || parsedQuantity1 == null) {
            ShowNotification("Error: Stock Quantity must have atleast 1 value.", true);
            return;
        }
        try {
            var currentItem = _fullInventoryList.FirstOrDefault(i => i.ItemCode == StockOutForm.ItemCode);
            if (currentItem != null && StockOutForm.Quantity > currentItem.Final_Stock) {
                ShowNotification($"Insufficient stock! Only {currentItem.Final_Stock} available.", true);
                return;
            }
            string savedItemCode = StockOutForm.ItemCode;
            _repository.ProcessTransaction(StockOutForm);
            LoadData();
            IsStockOutDialogVisible = false;

            if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                if (desktop.MainWindow is MainWindow mainWindow) {
                    mainWindow.ScrollToBottomOfLog();
                }
            }
            ShowNotification("Stock out request logged successfully.");
        } catch {
            ShowNotification("Error: Failed to log stock out.", true);
        }
    }

    // --- STOCK IN LOGIC ---

    [RelayCommand]
    private async Task OpenStockInDialog() {
        if (SelectedItem != null && SelectedItem.ItemCode.StartsWith("PENDING-")) {
            ShowNotification("Error: Please assign a valid Item Code before adding deliveries.", true);
            await Task.Delay(800);
            OpenEditDialog();
            return;
        }
        StockInForm = new StockTransactionLog {
            ItemCode = SelectedItem?.ItemCode ?? string.Empty,
            ItemDescription = SelectedItem?.Description ?? string.Empty,
            Date = System.DateTime.Now.ToString("dd-MMM-yy HH:mm"),
            TransactionType = "IN",
            Quantity = 1,
            Remarks = string.Empty
        };
        IsStockInDialogVisible = true;
    }

    [RelayCommand]
    private void CloseStockInDialog() => IsStockInDialogVisible = false;

    [RelayCommand]
    private void SaveStockIn() {
        if (string.IsNullOrWhiteSpace(StockInForm.ItemCode)) {
            ShowNotification("Error: Please select an item code!", true);
            return;
        }
        if (string.IsNullOrWhiteSpace(StockInForm.NameRequested)) {
            ShowNotification("Error: Name of Deliverer is required!", true);
            return;
        }
        if (!int.TryParse(StockInForm.QuantityUI, out int parsedQuantity) || parsedQuantity < 0) {
            ShowNotification("Error: Stock Quantity must be a valid number (e.g., 0, 10, 50).", true);
            return;
        }
        if (!int.TryParse(StockInForm.QuantityUI, out int parsedQuantity1) || parsedQuantity1 == 0 || parsedQuantity1 == null) {
            ShowNotification("Error: Stock Quantity must have atleast 1 value.", true);
            return;
        }
        try {
            string savedItemCode = StockInForm.ItemCode;
            _repository.ProcessTransaction(StockInForm);
            LoadData(); 
            IsStockInDialogVisible = false;
            
            if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                if (desktop.MainWindow is MainWindow mainWindow) {
                    mainWindow.ScrollToBottomOfLog();
                }
            }
            ShowNotification("Delivery (Stock In) added successfully.");
        } catch {
            ShowNotification("Error: Failed to add delivery.", true);
        }
    }

    [ObservableProperty]
    private bool _isDeleteLogDialogVisible;
    private System.Collections.IList _logsToDeletePending;

    [RelayCommand]
    private void OpenDeleteLogDialog(System.Collections.IList selectedLogs) {
        if (selectedLogs == null || selectedLogs.Count == 0) return;
        
        _logsToDeletePending = selectedLogs;
        IsDeleteLogDialogVisible = true;
    }

    [RelayCommand]
    private void CloseDeleteLogDialog() => IsDeleteLogDialogVisible = false;

    [RelayCommand]
    private void ConfirmDeleteLog() {
        if (_logsToDeletePending == null) return;

        var logsToDelete = _logsToDeletePending.Cast<StockTransactionLog>().ToList();
        foreach(var log in logsToDelete) {
            _repository.DeleteTransactionLog(log.TransactionId);
        }
        
        IsDeleteLogDialogVisible = false;
        LoadData();
        ShowNotification($"{logsToDelete.Count} log(s) deleted.");
    }

    [RelayCommand]
    private void HandleGlobalDelete() {
        if (SelectedTabIndex == 0) {
            if (IsDeleteDialogVisible) {
                ConfirmDelete();
            } else if (CurrentSelectedItems != null && CurrentSelectedItems.Count > 0) {
                // Pass the entire list of selected rows to the dialog!
                OpenDeleteDialog(CurrentSelectedItems);
            } else if (SelectedItem != null) {
                // Safe fallback if only one item is selected
                var list = new System.Collections.Generic.List < InventoryItem > {
                    SelectedItem
                };
                OpenDeleteDialog(list);
            }
        } else if (SelectedTabIndex == 1) {
            if (IsDeleteLogDialogVisible) {
                ConfirmDeleteLog();
            } else if (CurrentSelectedItems != null && CurrentSelectedItems.Count > 0) {
                OpenDeleteLogDialog(CurrentSelectedItems);
            } else if (SelectedLog != null) {
                var list = new System.Collections.Generic.List < StockTransactionLog > {
                    SelectedLog
                };
                OpenDeleteLogDialog(list);
            }
        }
    }

    public void UpdateItemInDatabase(InventoryItem item) {
        _repository.UpdateItem(item);
    }

    public void UpdateTransactionLogInDatabase(StockTransactionLog log) {
        _repository.UpdateTransactionLog(log);
        LoadData();
    }

    public async void ShowNotification(string message, bool isError = false) {
        SnackbarMessage = message;
        SnackbarColor = isError ? "#D93025" : "#323232";
        SnackbarOpacity = 1.0;

        await Task.Delay(3000);

        if (SnackbarMessage == message) {
            SnackbarOpacity = 0.0;
        }
    }

    // --- PROPERTIES FOR TOP-LEFT SNACKBAR ---
    private double _selectionSnackbarOpacity = 0.0;
    public double SelectionSnackbarOpacity {
        get => _selectionSnackbarOpacity;
        set {
            _selectionSnackbarOpacity = value;
            OnPropertyChanged(nameof(SelectionSnackbarOpacity));
        }
    }

    private string _selectionSnackbarMessage;
    public string SelectionSnackbarMessage {
        get => _selectionSnackbarMessage;
        set {
            _selectionSnackbarMessage = value;
            OnPropertyChanged(nameof(SelectionSnackbarMessage));
        }
    }

    private string _selectionSnackbarColor = "#1A73E8";
    public string SelectionSnackbarColor {
        get => _selectionSnackbarColor;
        set {
            _selectionSnackbarColor = value;
            OnPropertyChanged(nameof(SelectionSnackbarColor));
        }
    }

    public async void ShowSelectionNotification(string message, bool isError = false) {
        SelectionSnackbarMessage = message;
        // Blue for normal selection, Red if there's an error
        SelectionSnackbarColor = isError ? "#D93025" : "#1A73E8";
        // Fade in
        SelectionSnackbarOpacity = 1.0;
        // Wait 2.5 seconds
        await Task.Delay(2500);
        // Fade out
        SelectionSnackbarOpacity = 0.0;
    }
    
    private async Task<IStorageProvider?> GetStorageProvider() {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            return desktop.MainWindow?.StorageProvider;
        }
        return null;
    }

    // --- EXPORT TO EXCEL ---
    [ObservableProperty]
    private bool _isExportCompleteDialogVisible;
    private string _lastExportedFilePath = string.Empty;

    [RelayCommand]
    private void CloseExportDialog() {
        IsExportCompleteDialogVisible = false;
    }

    [RelayCommand]
    private void OpenExportedFile() {
        IsExportCompleteDialogVisible = false;

        if (!string.IsNullOrEmpty(_lastExportedFilePath) && System.IO.File.Exists(_lastExportedFilePath)) {
            try {
                // Setting UseShellExecute = true ensures this works across Windows, macOS, and Linux
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                    FileName = _lastExportedFilePath,
                    UseShellExecute = true 
                });
            } catch (Exception ex) {
                ShowNotification($"Could not open file: {ex.Message}", true);
                Log.Error($"Could not open file: {ex.Message}");
            }
        }
    }
    [RelayCommand]
    private async Task ExportDataAsync() {
        var storage = await GetStorageProvider();
        if (storage == null) return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions {
            Title = "Export Inventory Data",
            DefaultExtension = ".xlsx",
            SuggestedFileName = $"Inventory_Export_{DateTime.Now:yyyy-MM-dd}",
            FileTypeChoices = new[] {
                new FilePickerFileType("Excel Workbook") {
                    Patterns = new[] {
                        "*.xlsx"
                    }
                }
            }
        });

        if (file == null) return;
        IsExporting = true;
        try {
            string filePath = file.TryGetLocalPath() ?? string.Empty;
            if (string.IsNullOrEmpty(filePath)) return;

            await Task.Run(() => {
                using var workbook = new XLWorkbook();

                // ==========================================
                // 1. FORMAT INVENTORY SHEET
                // ==========================================
                var invSheet = workbook.Worksheets.Add("Inventory Stock");

                var logoCell = invSheet.Cell("B1");
                logoCell.GetRichText().AddText("DTI-").SetFontColor(XLColor.FromHtml("#1F497D")).SetBold().SetFontSize(16);
                logoCell.GetRichText().AddText("ISMS").SetFontColor(XLColor.Red).SetBold().SetFontSize(16);
                logoCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var deptCell = invSheet.Cell("D1");
                deptCell.Value = "INFORMATION SYSTEMS MANAGEMENT SERVICES";
                deptCell.Style.Font.FontColor = XLColor.FromHtml("#1F497D");
                deptCell.Style.Font.Bold = true;
                deptCell.Style.Font.FontSize = 16;

                var headerRange = invSheet.Range("A2:J2");
                headerRange.Merge();
                headerRange.Value = $"SUPPLIES INVENTORY MONITORING as of {DateTime.Now:MMMM yyyy}";
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#A9D08E");
                headerRange.Style.Font.FontColor = XLColor.FromHtml("#385723");
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Font.FontSize = 14;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                string[] invHeaders = {
                    "ITEM CODE",
                    "DESCRIPTION",
                    "AS OF",
                    "INITIAL STOCK",
                    "STOCK IN",
                    "STOCK OUT",
                    "FINAL STOCK",
                    "LOCATION",
                    "REMARKS",
                    "STATUS"
                };
                for (int i = 0; i < invHeaders.Length; i++) {
                    var cell = invSheet.Cell(3, i + 1);
                    cell.Value = invHeaders[i];
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
                    cell.Style.Font.FontColor = XLColor.FromHtml("#548235");
                    cell.Style.Font.Bold = true;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Border.OutsideBorderColor = XLColor.White;
                }

                for (int i = 0; i < _fullInventoryList.Count; i++) {
                    var item = _fullInventoryList[i];
                    int row = i + 4;

                    // --- CHANGED: Hides PENDING codes by outputting a blank string ---
                    invSheet.Cell(row, 1).Value = item.ItemCode != null && item.ItemCode.StartsWith("PENDING-") ? "" : item.ItemCode;
                    invSheet.Cell(row, 2).Value = item.Description;
                    invSheet.Cell(row, 3).Value = item.AsOfDate;
                    invSheet.Cell(row, 4).Value = item.InitialStock;
                    invSheet.Cell(row, 5).Value = item.Stock_In;
                    invSheet.Cell(row, 6).Value = item.Stock_Out;
                    invSheet.Cell(row, 7).Value = item.Final_Stock;
                    invSheet.Cell(row, 8).Value = item.Location;
                    invSheet.Cell(row, 9).Value = item.Remarks;
                    invSheet.Cell(row, 10).Value = item.Status;

                    var rowRange = invSheet.Range(row, 1, row, 10);
                    if (i % 2 == 0) rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E9EEF4");

                    rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    rowRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#CFD5DA");
                    rowRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CFD5DA");

                    invSheet.Cell(row, 1).Style.Font.Bold = true;
                    invSheet.Cell(row, 9).Style.Font.Bold = true;

                    if (!string.IsNullOrEmpty(item.Remarks) && item.Remarks.Contains("Reorder", StringComparison.OrdinalIgnoreCase)) {
                        invSheet.Cell(row, 10).Style.Font.FontColor = XLColor.Red;
                    }
                }
                invSheet.Columns().AdjustToContents();
                invSheet.Column(4).Width = 13;

                // ==========================================
                // 2. FORMAT TRANSACTION LOG SHEETS
                // ==========================================
                var logsByMonth = _fullTransactionLogs.GroupBy(log => {
                    if (DateTime.TryParse(log.Date, out DateTime d)) return d.ToString("MMM yyyy");
                    return "Unknown Date";
                }).ToList();

                string[] logHeaders = {
                    "DATE",
                    "NAME REQUESTED",
                    "DESCRIPTION",
                    "QUANTITY",
                    "TYPE (IN/OUT)",
                    "ITEM CODE",
                    "REMARKS"
                };

                foreach(var group in logsByMonth) {
                    var logSheet = workbook.Worksheets.Add($"Logs - {group.Key}");

                    var logLogoCell = logSheet.Cell("B1");
                    logLogoCell.GetRichText().AddText("DTI-").SetFontColor(XLColor.FromHtml("#1F497D")).SetBold().SetFontSize(16);
                    logLogoCell.GetRichText().AddText("ISMS").SetFontColor(XLColor.Red).SetBold().SetFontSize(16);
                    logLogoCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    var logDeptCell = logSheet.Cell("D1");
                    logDeptCell.Value = "TRANSACTION LOG HISTORY";
                    logDeptCell.Style.Font.FontColor = XLColor.FromHtml("#1F497D");
                    logDeptCell.Style.Font.Bold = true;
                    logDeptCell.Style.Font.FontSize = 16;

                    var logHeaderRange = logSheet.Range("A2:G2");
                    logHeaderRange.Merge();
                    logHeaderRange.Value = $"MONTHLY TRANSACTION RECORD - {group.Key.ToUpper()}";
                    logHeaderRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#A9D08E");
                    logHeaderRange.Style.Font.FontColor = XLColor.FromHtml("#385723");
                    logHeaderRange.Style.Font.Bold = true;
                    logHeaderRange.Style.Font.FontSize = 14;
                    logHeaderRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    logHeaderRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    for (int i = 0; i < logHeaders.Length; i++) {
                        var cell = logSheet.Cell(3, i + 1);
                        cell.Value = logHeaders[i];
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
                        cell.Style.Font.FontColor = XLColor.FromHtml("#548235");
                        cell.Style.Font.Bold = true;
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        cell.Style.Border.OutsideBorderColor = XLColor.White;
                    }

                    var monthlyLogs = group.ToList();
                    for (int i = 0; i < monthlyLogs.Count; i++) {
                        var log = monthlyLogs[i];
                        int row = i + 4;

                        logSheet.Cell(row, 1).Value = log.Date;
                        logSheet.Cell(row, 2).Value = log.NameRequested;
                        logSheet.Cell(row, 3).Value = log.ItemDescription;
                        logSheet.Cell(row, 4).Value = log.Quantity;
                        logSheet.Cell(row, 5).Value = log.TransactionType;
                        // --- CHANGED: Hides PENDING codes by outputting a blank string ---
                        logSheet.Cell(row, 6).Value = log.ItemCode != null && log.ItemCode.StartsWith("PENDING-") ? "" : log.ItemCode;
                        logSheet.Cell(row, 7).Value = log.Remarks;

                        var rowRange = logSheet.Range(row, 1, row, 7);
                        if (i % 2 == 0) rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E9EEF4");

                        rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                        rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        rowRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#CFD5DA");
                        rowRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CFD5DA");

                        if (log.TransactionType == "IN") logSheet.Cell(row, 5).Style.Font.FontColor = XLColor.FromHtml("#385723");
                        if (log.TransactionType == "OUT") logSheet.Cell(row, 5).Style.Font.FontColor = XLColor.Red;
                    }
                    logSheet.Columns().AdjustToContents();
                    logSheet.Column(4).Width = 10;
                }

                workbook.SaveAs(filePath);
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => {
                    ShowNotification("Excel export completed successfully!");
                    _lastExportedFilePath = filePath;
                    IsExportCompleteDialogVisible = true;
                });
                
                Serilog.Log.Information("Data exported to Excel at {FilePath}", filePath);
            });

        } catch (Exception ex) {
            ShowNotification("Error: " + ex.Message, true);
            Serilog.Log.Error(ex, "Failed to export data to Excel.");
        } finally {
            IsExporting = false;
        }
    }

    [RelayCommand]
    private async Task ImportDataAsync() {
        var storage = await GetStorageProvider();
        if (storage == null) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = "Import Inventory Data",
            AllowMultiple = false,
            FileTypeFilter = new[] {
                new FilePickerFileType("Excel Files") {
                    Patterns = new[] { "*.xlsx", "*.xlsm" }
                }
            }
        });

        if (files == null || files.Count == 0) return;
        
        string filePath = files[0].TryGetLocalPath() ?? string.Empty;
        if (string.IsNullOrEmpty(filePath)) return;

        await ImportDataFromFileAsync(filePath);
    }

    public async Task ImportDataFromFileAsync(string filePath) {
        IsImporting = true;
        try {
            int addedItems = 0;
            int updatedItems = 0;
            int addedLogs = 0;

            await Task.Run(() => {
                using var workbook = new XLWorkbook(filePath);
                var exactExcelItems = new System.Collections.Generic.List<InventoryItem>();

                // 1. IMPORT INVENTORY ITEMS
                if (workbook.TryGetWorksheet("Inventory Stock", out var invSheet)) {
                    var rows = invSheet.RowsUsed().Skip(3);

                    foreach(var row in rows) {
                        var excelItemCode = row.Cell(1).GetString();
                        var excelDesc = row.Cell(2).GetString();

                        // Skip completely blank rows
                        if (string.IsNullOrWhiteSpace(excelItemCode) && string.IsNullOrWhiteSpace(excelDesc)) continue;

                        // MATCHING LOGIC STEP 1: Try exact ItemCode match first
                        var existingItem = _fullInventoryList.FirstOrDefault(i => 
                            !string.IsNullOrWhiteSpace(excelItemCode) && i.ItemCode == excelItemCode);

                        if (existingItem == null && !string.IsNullOrWhiteSpace(excelDesc)) {
                            existingItem = _fullInventoryList.FirstOrDefault(i => i.Description == excelDesc);
                        }

                        string finalItemCode = excelItemCode;
                        bool isUpgradingCode = false;

                        // RESOLVE THE ITEM CODE
                        if (existingItem != null) {
                            if (string.IsNullOrWhiteSpace(excelItemCode)) {
                                // Re-importing a blank item. Reuse the DB's existing code to prevent duplicates!
                                finalItemCode = existingItem.ItemCode;
                            } 
                            else if (existingItem.ItemCode.StartsWith("PENDING-") && !string.IsNullOrWhiteSpace(excelItemCode)) {
                                // Upgrading a PENDING code to a REAL Excel code
                                finalItemCode = excelItemCode;
                                isUpgradingCode = true; 
                            }
                        } 
                        else if (string.IsNullOrWhiteSpace(excelItemCode)) {
                            // Completely new item with no code
                            string randomSuffix = Guid.NewGuid().ToString("N").Substring(0, 4).ToUpper();
                            finalItemCode = $"PENDING-{DateTime.Now:MMddHHmmss}-{randomSuffix}";
                        }

                        // Build the item
                        var item = new InventoryItem {
                            ItemCode = finalItemCode,
                            Description = excelDesc,
                            AsOfDate = row.Cell(3).GetString(),
                            InitialStock = row.Cell(4).TryGetValue<int>(out int initial) ? initial : 0,
                            Stock_In = row.Cell(5).TryGetValue<int>(out int inStock) ? inStock : 0,
                            Stock_Out = row.Cell(6).TryGetValue<int>(out int outStock) ? outStock : 0,
                            Final_Stock = row.Cell(7).TryGetValue<int>(out int final) ? final : 0,
                            Location = row.Cell(8).GetString(),
                            Remarks = row.Cell(9).GetString(),
                            Status = row.Cell(10).GetString()
                        };
                        exactExcelItems.Add(item);

                        if (isUpgradingCode) {
                            // Safest way to change a Primary Key: Remove the old PENDING row, add the REAL row
                            _repository.DeleteItem(existingItem.ItemCode); // Make sure your repository has a DeleteItem method!
                            _repository.AddItem(item);
                            updatedItems++;
                        } 
                        else if (existingItem != null) {
                            _repository.UpdateItem(item);
                            updatedItems++;
                        } 
                        else {
                            _repository.AddItem(item);
                            addedItems++;
                        }
                    }
                }

                // 2. IMPORT TRANSACTION LOGS
                foreach(var sheet in workbook.Worksheets) {
                    if (sheet.Name.StartsWith("Logs - ")) {
                        var logRows = sheet.RowsUsed().Skip(3);

                        foreach(var row in logRows) {
                            var date = row.Cell(1).GetString();
                            if (string.IsNullOrWhiteSpace(date)) {
                                date = string.Empty; 
                            }

                            var itemCode = row.Cell(6).GetString();
                            var description = row.Cell(3).GetString();

                            // Skip completely empty "ghost" rows
                            if (string.IsNullOrWhiteSpace(itemCode) && string.IsNullOrWhiteSpace(description)) {
                                continue; 
                            }

                            if (string.IsNullOrWhiteSpace(itemCode)) {
                                string randomSuffix = Guid.NewGuid().ToString("N").Substring(0, 4).ToUpper();
                                itemCode = $"PENDING-LOG-{DateTime.Now:MMddHHmmss}-{randomSuffix}";
                            }

                            var log = new StockTransactionLog {
                                Date = date,
                                NameRequested = row.Cell(2).GetString(),
                                ItemDescription = description,
                                Quantity = row.Cell(4).TryGetValue<int>(out int q) ? q : 0,
                                TransactionType = row.Cell(5).GetString(),
                                ItemCode = itemCode,
                                Remarks = row.Cell(7).GetString()
                            };

                            bool isDuplicate = _fullTransactionLogs.Any(existing =>
                                existing.Date == log.Date &&
                                existing.ItemCode == log.ItemCode &&
                                existing.TransactionType == log.TransactionType &&
                                existing.Quantity == log.Quantity &&
                                existing.NameRequested == log.NameRequested);

                            if (!isDuplicate) {
                                _repository.ProcessTransaction(log);
                                addedLogs++;
                            }
                        }
                    }
                }
                if (addedLogs > 0) {
                    foreach (var originalItem in exactExcelItems) {
                        _repository.UpdateItem(originalItem);
                    }
                }
            });

            LoadData();
            ShowNotification($"Import success: {addedItems} added, {updatedItems} updated. {addedLogs} logs imported.");

        } catch (Exception ex) {
            ShowNotification("Error: " + ex.Message, true);
            Serilog.Log.Error(ex, "Failed to import data from Excel.");
        } finally {
            IsImporting = false;
        }
    }
}