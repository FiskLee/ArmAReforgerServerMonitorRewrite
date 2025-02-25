using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Serilog;

namespace ArmaReforgerServerMonitor.Frontend
{
    public partial class FrontendLogsWindow : Window
    {
        private DispatcherTimer _refreshTimer;

        public FrontendLogsWindow()
        {
            InitializeComponent();
            // Log the file path for debugging.
            Log.Debug("FrontendLogsWindow: Using log file path: {LogFilePath}", App.LogFilePath);
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // Read the log file using the same absolute path.
                if (File.Exists(App.LogFilePath))
                {
                    string content = File.ReadAllText(App.LogFilePath);
                    // Log the length for debugging.
                    Log.Debug("FrontendLogsWindow: Read log file content length: {Length}", content.Length);
                    // Update the TextBox.
                    LogsTextBox.Text = string.IsNullOrWhiteSpace(content)
                        ? "Log file exists but is empty."
                        : content;
                }
                else
                {
                    LogsTextBox.Text = "No frontend logs available.";
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error reading frontend log file: {Message}", ex.Message);
                LogsTextBox.Text = "Error reading log file.";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _refreshTimer.Stop();
            base.OnClosed(e);
        }
    }
}
