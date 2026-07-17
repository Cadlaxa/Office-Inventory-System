using System;
using System.Linq;
using Avalonia.Data;

namespace Office_Supplies_Inventory {
    // Model for the SUPPLIES INVENTORY MONITORING table
    public class InventoryItem {
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
        public string ManufacturerSupplier { get; set; } = string.Empty;
        public string AsOfDate { get; set; } = string.Empty;
        public int InitialStock { get; set; }
        private string _initialStockUI;
        public string InitialStockUI {
            get => _initialStockUI;
            set {
                if (string.IsNullOrWhiteSpace(value)) {
                    throw new DataValidationException("Stock value cannot be blank.");
                }
                if (!int.TryParse(value, out int parsedStock)) {
                    throw new DataValidationException("Stock must be a valid number.");
                }
                if (parsedStock > 500) {
                    throw new DataValidationException("Maximum stock limit is 500.");
                }
                if (parsedStock < 0) {
                    throw new DataValidationException("Stock value cannot be negative.");
                }

                _initialStockUI = value;
                InitialStock = parsedStock; 
            }
        }
        public int Stock_In { get; set; }
        public int Stock_Out { get; set; }
        public int Final_Stock { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    // Model for the STOCK IN/OUT MONITORING table
    public class StockTransactionLog {
        public int TransactionId { get; set; }
        public string Date { get; set; } = string.Empty;
        public string NameRequested { get; set; } = string.Empty;
        public string ItemDescription { get; set; } = string.Empty;
        public int Quantity { get; set; }
        private string _QuantityUI;
        public string QuantityUI {
            get => _QuantityUI;
            set {
                if (string.IsNullOrWhiteSpace(value)) {
                    throw new DataValidationException("Quantity value cannot be blank.");
                }
                if (!int.TryParse(value, out int parsedQuantity)) {
                    throw new DataValidationException("Quantity must be a valid number.");
                }
                if (parsedQuantity > 500) {
                    throw new DataValidationException("Maximum Quantity limit is 500.");
                }
                if (parsedQuantity < 0) {
                    throw new DataValidationException("Quantity value cannot be negative.");
                }

                _QuantityUI = value;
                Quantity = parsedQuantity; 
            }
        }
        public string TransactionType { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public InventoryItem Item { get; set; }
    }
}