using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using MahApps.Metro.Controls;
using Serilog;
using BattleNET;


namespace ArmaReforgerServerMonitor.Frontend
{
    public partial class FrontendLogsWindow : MetroWindow
    {
        private DispatcherTimer _refreshTimer;
        private readonly string _logsFolder;
        private string _currentLogFilePath = string.Empty;

        public FrontendLogsWindow()
        {
            InitializeComponent();

            // Determine the absolute logs folder path.
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _logsFolder = Path.Combine(baseDir, "logs");

            // Ensure the logs folder exists.
            Directory.CreateDirectory(_logsFolder);

            // Attempt to locate the most recent frontend log file.
            UpdateCurrentLogFilePath();

            Log.Information("FrontendLogsWindow initialized. Using log file: {LogFilePath}", _currentLogFilePath);

            // Initialize the timer with an interval of 2 seconds.
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();
        }

        /// <summary>
        /// Searches the logs folder for files matching "frontend_log*.json" and sets _currentLogFilePath
        /// to the most recently modified one.
        /// </summary>
        private void UpdateCurrentLogFilePath()
        {
            try
            {
                var logFiles = Directory.GetFiles(_logsFolder, "frontend_log*.json");
                if (logFiles.Any())
                {
                    // Get the most recent file by LastWriteTime.
                    _currentLogFilePath = logFiles
                        .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                        .First();
                }
                else
                {
                    _currentLogFilePath = string.Empty;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error finding frontend log file: {Message}", ex.Message);
                _currentLogFilePath = string.Empty;
            }
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                // Update current log file path (in case a new file was created).
                UpdateCurrentLogFilePath();

                if (string.IsNullOrEmpty(_currentLogFilePath) || !File.Exists(_currentLogFilePath))
                {
                    LogsTextBox.Text = $"Log file not found in {_logsFolder}";
                    Log.Debug("Log file not found in {LogsFolder}", _logsFolder);
                    return;
                }

                // Open the file with shared read access.
                using (var stream = new FileStream(_currentLogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    string logs = reader.ReadToEnd();
                    LogsTextBox.Text = logs;
                }
                Log.Debug("Log file read successfully from {LogFilePath}", _currentLogFilePath);
            }
            catch (Exception ex)
            {
                Log.Error("Error reading log file in FrontendLogsWindow: {Message}", ex.Message);
                LogsTextBox.Text = "Error reading log file: " + ex.Message;
            }
        }
    }
}
