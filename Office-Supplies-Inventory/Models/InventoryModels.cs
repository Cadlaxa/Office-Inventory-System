using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Data;

namespace Office_Supplies_Inventory {
    // Model for the SUPPLIES INVENTORY MONITORING table
    public class InventoryItem : INotifyPropertyChanged {
        private string _itemCode;
        public string ItemCode {
            get => _itemCode;
            set {
                if (!string.IsNullOrWhiteSpace(value) && 
                    !value.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '/')) {
                    throw new DataValidationException("Item Code can only contain letters, numbers, hyphens, and slashes.");
                }
                _itemCode = value;
            }
        }
        public string Description { get; set; } = string.Empty;
        public string AsOfDate { get; set; } = string.Empty;
        private int _initialStock;
        public int InitialStock { 
            get => _initialStock; 
            set {
                if (_initialStock != value) {
                    _initialStock = value;
                    OnPropertyChanged(nameof(InitialStock));
                }
            }
        }

        private string _initialStockUI;
        public string InitialStockUI {
            get => _initialStockUI;
            set {
                if (string.IsNullOrWhiteSpace(value)) {
                    throw new Avalonia.Data.DataValidationException("Stock value cannot be blank.");
                }
                if (!int.TryParse(value, out int parsedStock)) {
                    throw new Avalonia.Data.DataValidationException("Stock must be a valid number.");
                }
                if (parsedStock > 5000) {
                    throw new Avalonia.Data.DataValidationException("Maximum Stock limit is 5000.");
                }
                if (parsedStock < 0) {
                    throw new Avalonia.Data.DataValidationException("Stock value cannot be negative.");
                }

                if (_initialStockUI != value) {
                    _initialStockUI = value;
                    OnPropertyChanged(nameof(InitialStockUI)); 
                    InitialStock = parsedStock; 
                }
            }
        }
        public int Stock_In { get; set; }
        public int Stock_Out { get; set; }
        public int Final_Stock { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Model for the STOCK IN/OUT MONITORING table
    public class StockTransactionLog : INotifyPropertyChanged {
        public int TransactionId { get; set; }
        public string Date { get; set; } = string.Empty;
        public string NameRequested { get; set; } = string.Empty;
        public string ItemDescription { get; set; } = string.Empty;
        private int _Quantity;
        public int Quantity { 
            get => _Quantity; 
            set {
                if (_Quantity != value) {
                    _Quantity = value;
                    OnPropertyChanged(nameof(Quantity));
                }
            }
        }

        private string _QuantityUI;
        public string QuantityUI {
            get => _QuantityUI;
            set {
                if (string.IsNullOrWhiteSpace(value)) {
                    throw new Avalonia.Data.DataValidationException("Quantity value cannot be blank.");
                }
                if (!int.TryParse(value, out int parsedQuantity)) {
                    throw new Avalonia.Data.DataValidationException("Quantity must be a valid number.");
                }
                if (parsedQuantity > 5000) {
                    throw new Avalonia.Data.DataValidationException("Maximum Quantity limit is 5000.");
                }
                if (parsedQuantity < 0) {
                    throw new Avalonia.Data.DataValidationException("Quantity value cannot be negative.");
                }

                if (_QuantityUI != value) {
                    _QuantityUI = value;
                    OnPropertyChanged(nameof(QuantityUI)); 
                    Quantity = parsedQuantity; 
                }
            }
        }
        public string TransactionType { get; set; } = string.Empty;
        public string TransactionColor => TransactionType == "IN" ? "#107C41" : "#D13438";

        public string ItemCode { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public InventoryItem Item { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}