using System;

namespace Office_Supplies_Inventory {
    // Model for the SUPPLIES INVENTORY MONITORING table
    public class InventoryItem {
        public string ItemCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ManufacturerSupplier { get; set; } = string.Empty;
        public string AsOfDate { get; set; } = string.Empty;
        public int InitialStock { get; set; } 
        public int StockIn { get; set; }
        public int StockOut { get; set; }
        public int FinalStock { get; set; }
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
        public string TransactionType { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public InventoryItem Item { get; set; }
    }
}