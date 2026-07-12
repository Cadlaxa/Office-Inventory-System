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

namespace Office_Supplies_Inventory;

public partial class MainViewModel: ObservableObject {
    private readonly InventoryRepository _repository = new();

    [ObservableProperty]
    private ObservableCollection < InventoryItem > _inventoryList;

    [ObservableProperty]
    private InventoryItem _selectedItem;

    [ObservableProperty]
    private ObservableCollection < StockTransactionLog > _transactionLogs;

    [ObservableProperty]
    private bool _isAddDialogVisible;

    [ObservableProperty]
    private InventoryItem _newItemForm = new();

    [ObservableProperty]
    private ObservableCollection < InventoryItem > _allInventoryItems = new();

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

    private List < InventoryItem > _fullInventoryList = new();
    private List < StockTransactionLog > _fullTransactionLogs = new();

    [ObservableProperty]
    private int _selectedTabIndex = 0; 

    [RelayCommand]
    private void SwitchTab(string index) {
        if (int.TryParse(index, out int newIndex)) {
            SelectedTabIndex = newIndex;
        }
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

    public MainViewModel() {
        // Safe, lightweight initialization
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
        if (string.IsNullOrWhiteSpace(SearchQuery)) {
            InventoryList = new ObservableCollection < InventoryItem > (_fullInventoryList);
            TransactionLogs = new ObservableCollection < StockTransactionLog > (_fullTransactionLogs);
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

        InventoryList = new ObservableCollection < InventoryItem > (filteredInventory);

        // Filter Transaction Logs
        var filteredLogs = _fullTransactionLogs.Where(log =>
            (log.ItemCode?.ToLower().Contains(query) == true) ||
            (log.ItemDescription?.ToLower().Contains(query) == true) ||
            (log.NameRequested?.ToLower().Contains(query) == true) ||
            (log.TransactionType?.ToLower().Contains(query) == true) ||
            (log.Date?.ToLower().Contains(query) == true) ||
            (log.Remarks?.ToLower().Contains(query) == true)
        ).ToList();

        TransactionLogs = new ObservableCollection < StockTransactionLog > (filteredLogs);
    }

    [RelayCommand]
    private void OpenAddDialog() {
        NewItemForm = new InventoryItem {
            ItemCode = string.Empty,
                Description = string.Empty,
                ManufacturerSupplier = string.Empty,
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
        if (string.IsNullOrWhiteSpace(NewItemForm.ItemCode)) {
            ShowNotification("Error: Item Code is required!", true);
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

    [RelayCommand]
    private void DeleteSelectedItems(System.Collections.IList selectedItems) {
        if (selectedItems == null || selectedItems.Count == 0) return;

        var itemsToDelete = selectedItems.Cast < InventoryItem > ().ToList();
        foreach(var item in itemsToDelete) {
            _repository.DeleteItem(item.ItemCode);
        }
        LoadData();
        ShowNotification($"{itemsToDelete.Count} item(s) deleted.");
    }

    [RelayCommand]
    private void OpenStockOutDialog() {
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
        try {
            var currentItem = _fullInventoryList.FirstOrDefault(i => i.ItemCode == StockOutForm.ItemCode);
            if (currentItem != null && StockOutForm.Quantity > currentItem.Final_Stock) {
                ShowNotification($"Insufficient stock! Only {currentItem.Final_Stock} available.", true);
                return;
            }

            _repository.ProcessTransaction(StockOutForm);
            LoadData();
            IsStockOutDialogVisible = false;
            ShowNotification("Stock out request logged successfully.");
        } catch {
            ShowNotification("Error: Failed to log stock out.", true);
        }
    }

    [RelayCommand]
    private void OpenStockInDialog() {
        StockInForm = new StockTransactionLog {
            ItemCode = SelectedItem?.ItemCode ?? string.Empty,
                ItemDescription = SelectedItem?.Description ?? string.Empty,
                Date = System.DateTime.Now.ToString("dd-MMM-yy"),
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
        try {
            _repository.ProcessTransaction(StockInForm);
            LoadData();
            IsStockInDialogVisible = false;
            ShowNotification("Delivery (Stock In) added successfully.");
        } catch {
            ShowNotification("Error: Failed to add delivery.", true);
        }
    }

    [RelayCommand]
    private void DeleteSelectedLogs(System.Collections.IList selectedLogs) {
        if (selectedLogs == null || selectedLogs.Count == 0) return;

        var logsToDelete = selectedLogs.Cast < StockTransactionLog > ().ToList();
        foreach(var log in logsToDelete) {
            _repository.DeleteTransactionLog(log.TransactionId);
        }
        LoadData();
        ShowNotification($"{logsToDelete.Count} log(s) deleted.");
    }

    public void UpdateItemInDatabase(InventoryItem item) {
        _repository.UpdateItem(item);
    }

    public void UpdateTransactionLogInDatabase(StockTransactionLog log) {
        _repository.UpdateTransactionLog(log);
        LoadData();
    }

    private async void ShowNotification(string message, bool isError = false) {
        SnackbarMessage = message;
        SnackbarColor = isError ? "#D93025" : "#323232";
        SnackbarOpacity = 1.0;

        await Task.Delay(3000);

        if (SnackbarMessage == message) {
            SnackbarOpacity = 0.0;
        }
    }

    private async Task < IStorageProvider ? > GetStorageProvider() {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            return desktop.MainWindow?.StorageProvider;
        }
        return null;
    }

    // --- EXPORT TO EXCEL ---
    [RelayCommand]
    private async Task ExportDataAsync() {
        var storage = await GetStorageProvider();
        if (storage == null) return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions {
            Title = "Export Inventory Data",
                DefaultExtension = ".xlsx",
                SuggestedFileName = $"Inventory_Export_{DateTime.Now:yyyy-MM-dd}",
                FileTypeChoices = new [] {
                    new FilePickerFileType("Excel Workbook") {
                        Patterns = new [] {
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
                using
                var workbook = new XLWorkbook();

                // ==========================================
                // 1. FORMAT INVENTORY SHEET
                // ==========================================
                var invSheet = workbook.Worksheets.Add("Inventory Stock");

                var logoCell = invSheet.Cell("B1");
                logoCell.GetRichText().AddText("DTI-").SetFontColor(XLColor.FromHtml("#1F497D")).SetBold();
                logoCell.GetRichText().AddText("ISMS").SetFontColor(XLColor.Red).SetBold();
                logoCell.Style.Font.FontSize = 16;
                logoCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var deptCell = invSheet.Cell("E1");
                deptCell.Value = "INFORMATION SYSTEMS MANAGEMENT SERVICES";
                deptCell.Style.Font.FontColor = XLColor.FromHtml("#1F497D");
                deptCell.Style.Font.Bold = true;
                deptCell.Style.Font.FontSize = 16;

                var headerRange = invSheet.Range("A2:K2");
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
                    "MANUFACTURER / SUPPLIER",
                    "AS OF",
                    "INITIAL STOCK",
                    "STOCK IN",
                    "STOCK OUT",
                    "FINAL STOCK",
                    "LOCATION",
                    "REMARK'S",
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

                    invSheet.Cell(row, 1).Value = item.ItemCode;
                    invSheet.Cell(row, 2).Value = item.Description;
                    invSheet.Cell(row, 3).Value = item.ManufacturerSupplier;
                    invSheet.Cell(row, 4).Value = item.AsOfDate;
                    invSheet.Cell(row, 5).Value = item.InitialStock;
                    invSheet.Cell(row, 6).Value = item.Stock_In;
                    invSheet.Cell(row, 7).Value = item.Stock_Out;
                    invSheet.Cell(row, 8).Value = item.Final_Stock;
                    invSheet.Cell(row, 9).Value = item.Location;
                    invSheet.Cell(row, 10).Value = item.Remarks;
                    invSheet.Cell(row, 11).Value = item.Status;

                    var rowRange = invSheet.Range(row, 1, row, 11);
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
                    logLogoCell.GetRichText().AddText("DTI-").SetFontColor(XLColor.FromHtml("#1F497D")).SetBold();
                    logLogoCell.GetRichText().AddText("ISMS").SetFontColor(XLColor.Red).SetBold();
                    logLogoCell.Style.Font.FontSize = 18;
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
                        logSheet.Cell(row, 6).Value = log.ItemCode;
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
                }

                workbook.SaveAs(filePath);
            });
            ShowNotification("Excel export completed successfully!");

        } catch (Exception ex) {
            ShowNotification("Error: Could not save Excel file.", true);
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
                FileTypeFilter = new [] {
                    new FilePickerFileType("Excel Files") {
                        Patterns = new [] {
                            "*.xlsx"
                        }
                    }
                }
        });

        if (files == null || files.Count == 0) return;
        IsImporting = true;
        try {
            string filePath = files[0].TryGetLocalPath() ?? string.Empty;
            if (string.IsNullOrEmpty(filePath)) return;

            int addedItems = 0;
            int updatedItems = 0;
            int addedLogs = 0;

            await Task.Run(() => {
                using
                var workbook = new XLWorkbook(filePath);

                // 1. IMPORT INVENTORY ITEMS
                if (workbook.TryGetWorksheet("Inventory Stock", out
                        var invSheet)) {
                    var rows = invSheet.RowsUsed().Skip(3);

                    foreach(var row in rows) {
                        var itemCode = row.Cell(1).GetString();
                        if (string.IsNullOrWhiteSpace(itemCode)) continue;

                        var item = new InventoryItem {
                            ItemCode = itemCode,
                                Description = row.Cell(2).GetString(),
                                ManufacturerSupplier = row.Cell(3).GetString(),
                                AsOfDate = row.Cell(4).GetString(),
                                InitialStock = row.Cell(5).TryGetValue < int > (out int initial) ? initial : 0,
                                Stock_In = row.Cell(6).TryGetValue < int > (out int inStock) ? inStock : 0,
                                Stock_Out = row.Cell(7).TryGetValue < int > (out int outStock) ? outStock : 0,
                                Final_Stock = row.Cell(8).TryGetValue < int > (out int final) ? final : 0,
                                Location = row.Cell(9).GetString(),
                                Remarks = row.Cell(10).GetString(),
                                Status = row.Cell(11).GetString()
                        };

                        var existingItem = _fullInventoryList.FirstOrDefault(i => i.ItemCode == item.ItemCode);
                        if (existingItem != null) {
                            _repository.UpdateItem(item);
                            updatedItems++;
                        } else {
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
                            var itemCode = row.Cell(6).GetString();

                            if (string.IsNullOrWhiteSpace(date) || string.IsNullOrWhiteSpace(itemCode)) continue;

                            var log = new StockTransactionLog {
                                Date = date,
                                    NameRequested = row.Cell(2).GetString(),
                                    ItemDescription = row.Cell(3).GetString(),
                                    Quantity = row.Cell(4).TryGetValue < int > (out int q) ? q : 0,
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
            });

            LoadData();
            ShowNotification($"Import success: {addedItems} items added, {updatedItems} updated. {addedLogs} logs imported.");

        } catch (Exception ex) {
            ShowNotification("Error: Could not read the Excel file.", true);
            Serilog.Log.Error(ex, "Failed to import data from Excel.");
        } finally {
            IsImporting = false;
        }
    }
}