using Avalonia;
using Serilog;
using System;

namespace Office_Supplies_Inventory;

class Program {
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("logs/inventory-app-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try {
            Log.Information("Starting Office Supplies Inventory Application...");
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        } catch (Exception ex) {
            Log.Fatal(ex, "Application terminated unexpectedly");
        } finally {
            Log.CloseAndFlush();
        }
    }
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure < App > ()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}