using System;
using System.IO;
using System.Windows;
using Serilog;

namespace ArmaReforgerServerMonitor.Frontend
{
    public partial class App : Application
    {
        // This property holds the path to the log file.
        // It is assigned a default value so that it is never null.
        public string LogFilePath { get; set; } = "logs/frontend_log.json";

        protected override void OnStartup(StartupEventArgs e)
        {
            // Determine the absolute path for the logs folder.
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string logsDir = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(logsDir);

            // Set the absolute log file path.
            LogFilePath = Path.Combine(logsDir, "frontend_log.json");

            // Configure Serilog to write to the console and to the file with shared access.
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(LogFilePath, rollingInterval: RollingInterval.Day, shared: true)
                .CreateLogger();

            Log.Information("Frontend application starting up. Test log entry at {Time}", DateTime.Now);

            // (Optional) Show log file path for debugging.
            MessageBox.Show("Log file path: " + LogFilePath, "Debug: Log File Path");

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
