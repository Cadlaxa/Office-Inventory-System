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
                Stock_In INTEGER,
                Stock_Out INTEGER,
                Final_Stock INTEGER,
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
            
            // 1. UPDATE THE SPECIFIC TRANSACTION LOG
            // This updates the unique details (Quantity, Date, Remarks, the specific Requester) 
            // for the single row the user just edited.
            string updateLogSql = @"UPDATE StockTransactionLog
                                    SET Date = @Date,
                                        NameRequested = @NameRequested,
                                        ItemDescription = @ItemDescription,
                                        Quantity = @Quantity,
                                        TransactionType = @TransactionType,
                                        Remarks = @Remarks
                                    WHERE TransactionId = @TransactionId";
                                    
            db.Execute(updateLogSql, log);

            // 2. SYNC SHARED ITEM DETAILS ACROSS ALL LOGS
            // This ensures every single log with this ItemCode gets the corrected Description.
            string syncLogsSql = @"UPDATE StockTransactionLog
                                SET ItemDescription = @ItemDescription
                                WHERE ItemCode = @ItemCode";
                                
            db.Execute(syncLogsSql, new { 
                ItemDescription = log.ItemDescription, 
                ItemCode = log.ItemCode 
            });

            // 3. UPDATE INVENTORY AND RECALCULATE STOCK
            string updateInventorySql = @"
                UPDATE InventoryRecords 
                SET Description = @ItemDescription,
                    
                    Stock_In = COALESCE((SELECT SUM(Quantity) FROM StockTransactionLog 
                                        WHERE ItemCode = @ItemCode AND TransactionType = 'IN'), 0),
                    
                    Stock_Out = COALESCE((SELECT SUM(Quantity) FROM StockTransactionLog 
                                        WHERE ItemCode = @ItemCode AND TransactionType = 'OUT'), 0),
                    
                    Final_Stock = InitialStock + 
                                COALESCE((SELECT SUM(Quantity) FROM StockTransactionLog 
                                            WHERE ItemCode = @ItemCode AND TransactionType = 'IN'), 0) - 
                                COALESCE((SELECT SUM(Quantity) FROM StockTransactionLog 
                                            WHERE ItemCode = @ItemCode AND TransactionType = 'OUT'), 0)
                
                WHERE ItemCode = @ItemCode";
                
            db.Execute(updateInventorySql, new { 
                ItemDescription = log.ItemDescription, 
                ItemCode = log.ItemCode 
            });
            
            Serilog.Log.Information("Successfully updated transaction {TransactionId}, synced descriptions, and recalculated stock for item {ItemCode}.", log.TransactionId, log.ItemCode);
        }
        catch (Exception ex) {
            Serilog.Log.Error(ex, "Failed to update transaction {TransactionId} and recalculate stock for item {ItemCode}.", log.TransactionId, log.ItemCode);
            throw; 
        }
    }

    public void DeleteTransactionLog(int transactionId) {
        try {
            using IDbConnection db = new SqliteConnection(_connectionString);
            
            var log = db.QueryFirstOrDefault<StockTransactionLog>(
                "SELECT * FROM StockTransactionLog WHERE TransactionId = @TransactionId", 
                new { TransactionId = transactionId });
            
            db.Execute("DELETE FROM StockTransactionLog WHERE TransactionId = @TransactionId", new { TransactionId = transactionId });
            Log.Information("Successfully deleted transaction log ID: {TransactionId}", transactionId);

            if (log != null) {
                RecalculateInventoryStock(db, log.ItemCode);
            }
        } 
        catch (Exception ex) {
            Log.Error(ex, "Failed to delete transaction log!");
        }
    }

    private void RecalculateInventoryStock(IDbConnection db, string itemCode) {
        string updateSql = @"
            UPDATE InventoryRecords
            SET Stock_In = (SELECT COALESCE(SUM(Quantity), 0) FROM StockTransactionLog WHERE ItemCode = @ItemCode AND TransactionType = 'IN'),
                Stock_Out = (SELECT COALESCE(SUM(Quantity), 0) FROM StockTransactionLog WHERE ItemCode = @ItemCode AND TransactionType = 'OUT')
            WHERE ItemCode = @ItemCode;

            UPDATE InventoryRecords 
            SET Final_Stock = InitialStock + Stock_In - Stock_Out
            WHERE ItemCode = @ItemCode;
        ";
        db.Execute(updateSql, new { ItemCode = itemCode });
    }

    // CREATE
    public void AddItem(InventoryItem item) {
        try{
            using IDbConnection db = new SqliteConnection(_connectionString);
            string sql = @"INSERT INTO InventoryRecords (ItemCode, Description, ManufacturerSupplier, AsOfDate, InitialStock, Stock_In, Stock_Out, Final_Stock, Location, Remarks, Status) 
                           VALUES (@ItemCode, @Description, @ManufacturerSupplier, @AsOfDate, @InitialStock, @Stock_In, @Stock_Out, @Final_Stock, @Location, @Remarks, @Status)";
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
            
            // UPDATE THE MAIN INVENTORY ITEM
            string updateItemSql = @"UPDATE InventoryRecords 
                                    SET Description = @Description, 
                                        ManufacturerSupplier = @ManufacturerSupplier, 
                                        AsOfDate = @AsOfDate,
                                        InitialStock = @InitialStock,
                                        Stock_In = @Stock_In,
                                        Stock_Out = @Stock_Out,
                                        Final_Stock = @Final_Stock,
                                        Location = @Location, 
                                        Remarks = @Remarks,
                                        Status = @Status 
                                    WHERE ItemCode = @ItemCode";
                                    
            db.Execute(updateItemSql, item);

            // CASCADING UPDATE: UPDATE ALL TRANSACTION LOGS FOR THIS ITEM
            string updateLogsSql = @"UPDATE StockTransactionLog
                                    SET ItemDescription = @Description
                                    WHERE ItemCode = @ItemCode";
                                    
            db.Execute(updateLogsSql, new { Description = item.Description, ItemCode = item.ItemCode });
            Log.Information("Successfully updated item {ItemCode} and its cascading transaction logs.", item.ItemCode);
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to update item {ItemCode} and its cascading transaction logs.", item.ItemCode);
            throw; 
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
            ? "UPDATE InventoryRecords SET Stock_In = Stock_In + @Qty WHERE ItemCode = @ItemCode"
            : "UPDATE InventoryRecords SET Stock_Out = Stock_Out + @Qty WHERE ItemCode = @ItemCode";
            
        db.Execute(updateCountsSql, new { Qty = log.Quantity, ItemCode = log.ItemCode });

        string recalculateFinalSql = @"
            UPDATE InventoryRecords 
            SET Final_Stock = (InitialStock + Stock_In - Stock_Out) 
            WHERE ItemCode = @ItemCode";
            
        db.Execute(recalculateFinalSql, new { ItemCode = log.ItemCode });
    }
}