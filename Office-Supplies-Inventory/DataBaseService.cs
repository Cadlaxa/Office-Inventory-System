using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using Microsoft.Data.Sqlite;
using Office_Supplies_Inventory;
using Serilog;

namespace Office_Supplies_Inventory;

public class InventoryRepository {
    private readonly string _connectionString = "Data Source=InventoryDb.sqlite";

    public InventoryRepository() {
        InitializeDatabase();
    }

    private void InitializeDatabase() {
        try {
            Log.Information("Verifying database tables...");
            using IDbConnection db = new SqliteConnection(_connectionString);

            string sql = @"
            CREATE TABLE IF NOT EXISTS InventoryRecords(
                ItemCode TEXT PRIMARY KEY,
                Description TEXT,
                ManufacturerSupplier TEXT,
                AsOfDate TEXT,
                InitialStock INTEGER,
                StockIn INTEGER,
                StockOut INTEGER,
                FinalStock INTEGER,
                Location TEXT,
                Remarks TEXT,
                Status TEXT
            );

            CREATE TABLE IF NOT EXISTS StockTransactionLog(
                TransactionId INTEGER PRIMARY KEY AUTOINCREMENT,
                Date TEXT,
                NameRequested TEXT,
                ItemDescription TEXT,
                Quantity INTEGER,
                TransactionType TEXT,
                ItemCode TEXT,
                Remarks TEXT
            );
            ";

            db.Execute(sql);
            Log.Information("Database verification complete.");
        } catch (Exception ex) {
            Log.Error(ex, "Failed to initialize the database tables!");
        }
    }

    // READ
    public List < InventoryItem > GetAllItems() {
        try {
            Log.Debug("Fetching all inventory items from database...");
            using IDbConnection db = new SqliteConnection(_connectionString);
            var items = db.Query < InventoryItem > ("SELECT * FROM InventoryRecords").ToList();
            Log.Debug("Successfully fetched {Count} items.", items.Count);
            return items;
        } catch (Exception ex) {
            Log.Error(ex, "Error occurred while fetching inventory items.");
            return new List < InventoryItem > ();
        }
    }

    // READ LOGS
    public List<StockTransactionLog> GetTransactionLogs() {
        try {
            using IDbConnection db = new SqliteConnection(_connectionString);
            var logs = db.Query<StockTransactionLog>("SELECT * FROM StockTransactionLog ORDER BY TransactionId ASC").ToList();
            return logs;
        } 
        catch (Exception ex) {
            Log.Error(ex, "Error occurred while fetching transaction logs.");
            return new List<StockTransactionLog>(); 
        }
    }

    // UPDATE TRANSACTION LOG
    public void UpdateTransactionLog(StockTransactionLog log) {
        try {
            using IDbConnection db = new SqliteConnection(_connectionString);
            string sql = @"UPDATE StockTransactionLog 
                           SET Date = @Date, 
                               NameRequested = @NameRequested, 
                               ItemDescription = @ItemDescription, 
                               Quantity = @Quantity, 
                               TransactionType = @TransactionType, 
                               ItemCode = @ItemCode, 
                               Remarks = @Remarks 
                           WHERE TransactionId = @TransactionId";
            db.Execute(sql, log);
        } 
        catch (Exception ex) {
            Log.Error(ex, "Failed to update transaction log!");
        }
    }

    // CREATE
    public void AddItem(InventoryItem item) {
        try{
            using IDbConnection db = new SqliteConnection(_connectionString);
            string sql = @"INSERT INTO InventoryRecords (ItemCode, Description, ManufacturerSupplier, AsOfDate, InitialStock, StockIn, StockOut, FinalStock, Location, Remarks, Status) 
                           VALUES (@ItemCode, @Description, @ManufacturerSupplier, @AsOfDate, @InitialStock, @StockIn, @StockOut, @FinalStock, @Location, @Remarks, @Status)";
            db.Execute(sql, item);
            Log.Information("Successfully added item {ItemCode}.", item.ItemCode);
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to add item! You likely tried to save a duplicate Item Code.");
        }
    }

    // UPDATE
    public void UpdateItem(InventoryItem item) {
        try {
            using IDbConnection db = new SqliteConnection(_connectionString);
            string sql = @"UPDATE InventoryRecords 
                           SET Description = @Description, 
                               ManufacturerSupplier = @ManufacturerSupplier, 
                               AsOfDate = @AsOfDate,
                               InitialStock = @InitialStock,
                               StockIn = @StockIn,
                               StockOut = @StockOut,
                               FinalStock = @FinalStock,
                               Location = @Location, 
                               Remarks = @Remarks,
                               Status = @Status 
                           WHERE ItemCode = @ItemCode";
            db.Execute(sql, item);
            Log.Information("Successfully updated item {ItemCode}.", item.ItemCode);
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to update item {ItemCode}.", item.ItemCode);
        }
    }

    // DELETE
    public void DeleteItem(string itemCode) {
        using IDbConnection db = new SqliteConnection(_connectionString);
        db.Execute("DELETE FROM InventoryRecords WHERE ItemCode = @ItemCode", new { ItemCode = itemCode });
    }

    // STOCK OPERATION (IN/OUT)
     public void ProcessTransaction(StockTransactionLog log) {
        using IDbConnection db = new SqliteConnection(_connectionString);
        
        string logSql = @"INSERT INTO StockTransactionLog (Date, NameRequested, ItemDescription, Quantity, TransactionType, ItemCode, Remarks) 
                          VALUES (@Date, @NameRequested, @ItemDescription, @Quantity, @TransactionType, @ItemCode, @Remarks)";
        db.Execute(logSql, log);

        string updateCountsSql = log.TransactionType == "IN" 
            ? "UPDATE InventoryRecords SET StockIn = StockIn + @Qty WHERE ItemCode = @ItemCode"
            : "UPDATE InventoryRecords SET StockOut = StockOut + @Qty WHERE ItemCode = @ItemCode";
            
        db.Execute(updateCountsSql, new { Qty = log.Quantity, ItemCode = log.ItemCode });

        string recalculateFinalSql = @"
            UPDATE InventoryRecords 
            SET FinalStock = (InitialStock + StockIn - StockOut) 
            WHERE ItemCode = @ItemCode";
            
        db.Execute(recalculateFinalSql, new { ItemCode = log.ItemCode });
    }

    public void DeleteTransactionLog(int transactionId) {
        try {
            using IDbConnection db = new SqliteConnection(_connectionString);
            db.Execute("DELETE FROM StockTransactionLog WHERE TransactionId = @TransactionId", new { TransactionId = transactionId });
            Log.Information("Successfully deleted transaction log ID: {TransactionId}", transactionId);
        } 
        catch (Exception ex) {
            Log.Error(ex, "Failed to delete transaction log!");
        }
    }
}