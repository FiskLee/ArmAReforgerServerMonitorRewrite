using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using LiveCharts;
using LiveCharts.Wpf;
using System.Collections.ObjectModel;
using Serilog;
using ArmaReforgerServerMonitor.Frontend.Models; // Ensure OSDataDTO and DiskMetricsViewModel are defined here

namespace ArmaReforgerServerMonitor.Frontend
{
    public partial class MainWindow : Window
    {
        private static readonly ILogger Logger = Log.ForContext<MainWindow>();
        private HttpClient _httpClient = new HttpClient();
        private DispatcherTimer _pollTimer;
        private bool _isConnected = false;
        private int _failureCount = 0;
        private const int MaxFailures = 3;

        // Chart series for various metrics.
        public SeriesCollection TotalCpuSeries { get; set; } = new SeriesCollection();
        public SeriesCollection CpuSeries { get; set; } = new SeriesCollection();
        public SeriesCollection MemorySeries { get; set; } = new SeriesCollection();
        public SeriesCollection FPSSeries { get; set; } = new SeriesCollection();
        public SeriesCollection FrameTimeSeries { get; set; } = new SeriesCollection();
        public SeriesCollection ActivePlayersSeries { get; set; } = new SeriesCollection();

        // Labels for the charts.
        public ObservableCollection<string> TotalCpuLabels { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> CpuLabels { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> MemoryLabels { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> FPSLabels { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> FrameTimeLabels { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> ActivePlayersLabels { get; set; } = new ObservableCollection<string>();

        // Formatters.
        public Func<double, string> TotalCpuFormatter { get; set; } = value => $"{value:N0}%";
        public Func<double, string> CpuFormatter { get; set; } = value => $"{value:N0}%";
        public Func<double, string> MemoryFormatter { get; set; } = value => $"{value:N2} GB";
        public Func<double, string> FPSFormatter { get; set; } = value => $"{value:N0}";
        public Func<double, string> FrameTimeFormatter { get; set; } = value => $"{value:N0} ms";
        public Func<double, string> ActivePlayersFormatter { get; set; } = value => $"{value:N0}";

        // Summary and status properties.
        public string PerformanceSummary { get; set; } = "No data";
        public string ConsoleLogSummary { get; set; } = "No console log entries parsed yet";
        public string Status { get; set; } = "Disconnected";

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _pollTimer.Tick += async (s, e) => await PollBackend();

            Logger.Information("MainWindow initialized. Test log entry.");
        }

        private async Task PollBackend()
        {
            if (!_isConnected)
                return;

            try
            {
                // Poll OS metrics endpoint.
                var osResponse = await _httpClient.GetAsync(ServerUrlTextBox.Text + "/api/data/osmetrics");
                Logger.Debug("OS Metrics API responded with status: {StatusCode}", osResponse.StatusCode);
                if (osResponse.IsSuccessStatusCode)
                {
                    _failureCount = 0; // Reset failure counter.
                    var json = await osResponse.Content.ReadAsStringAsync();
                    var osMetrics = JsonSerializer.Deserialize<OSDataDTO>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (osMetrics != null)
                    {
                        UpdateChartsAndSummary(osMetrics);
                    }
                }
                else
                {
                    _failureCount++;
                    Logger.Warning("OS Metrics API non-success status: {StatusCode} (Failure {FailureCount}/{MaxFailures})",
                        osResponse.StatusCode, _failureCount, MaxFailures);
                }

                // Poll console log statistics endpoint.
                var logResponse = await _httpClient.GetAsync(ServerUrlTextBox.Text + "/api/data/consolelogstats");
                Logger.Debug("Console log API responded with status: {StatusCode}", logResponse.StatusCode);
                if (logResponse.IsSuccessStatusCode)
                {
                    var logStats = await logResponse.Content.ReadAsStringAsync();
                    ConsoleLogSummary = logStats;
                    ConsoleLogSummaryTextBlock.Text = ConsoleLogSummary;
                }
                else
                {
                    ConsoleLogSummary = "No console log entries parsed yet";
                    ConsoleLogSummaryTextBlock.Text = ConsoleLogSummary;
                }

                if (_failureCount >= MaxFailures)
                {
                    await HandleConnectionLoss();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Exception during PollBackend: {Message}", ex.Message);
                _failureCount++;
                if (_failureCount >= MaxFailures)
                {
                    await HandleConnectionLoss();
                }
            }
        }

        private async Task HandleConnectionLoss()
        {
            _isConnected = false;
            Status = "Disconnected";
            StatusTextBlock.Text = Status;
            ConnectButton.IsEnabled = true;
            DisconnectButton.IsEnabled = false;
            _pollTimer.Stop();

            TotalCpuSeries.Clear();
            CpuSeries.Clear();
            MemorySeries.Clear();
            FPSSeries.Clear();
            FrameTimeSeries.Clear();
            ActivePlayersSeries.Clear();
            TotalCpuLabels.Clear();
            CpuLabels.Clear();
            MemoryLabels.Clear();
            FPSLabels.Clear();
            FrameTimeLabels.Clear();
            ActivePlayersLabels.Clear();

            PerformanceSummary = "No data";
            ConsoleLogSummary = "No console log entries parsed yet";
            PerformanceSummaryTextBlock.Text = PerformanceSummary;
            ConsoleLogSummaryTextBlock.Text = ConsoleLogSummary;

            await Task.CompletedTask;
        }

        private void UpdateChartsAndSummary(OSDataDTO metrics)
        {
            try
            {
                // Update Total CPU Chart.
                TotalCpuLabels.Clear();
                TotalCpuLabels.Add("Total CPU");
                TotalCpuSeries.Clear();
                TotalCpuSeries.Add(new ColumnSeries
                {
                    Title = "Total CPU",
                    Values = new ChartValues<double> { metrics.OverallCpuUsage },
                    DataLabels = true,
                    LabelPoint = point => $"{point.Y:N0}%"
                });

                // Update Per-Core CPU Chart.
                CpuLabels.Clear();
                foreach (var kvp in metrics.PerCoreCpuUsage)
                {
                    CpuLabels.Add(kvp.Key);
                }
                CpuSeries.Clear();
                CpuSeries.Add(new ColumnSeries
                {
                    Title = "Per-Core CPU",
                    Values = new ChartValues<double>(metrics.PerCoreCpuUsage.Values.Select(v => (double)v)),
                    DataLabels = true,
                    LabelPoint = point => $"{point.Y:N0}%"
                });

                // Update Memory Chart.
                MemoryLabels.Clear();
                MemoryLabels.Add("Used Memory");
                MemorySeries.Clear();
                MemorySeries.Add(new ColumnSeries
                {
                    Title = "Memory Used",
                    Values = new ChartValues<double> { metrics.MemoryUsedGB },
                    DataLabels = true,
                    LabelPoint = point => $"{point.Y:N2} GB"
                });

                // Update FPS Chart.
                FPSLabels.Clear();
                FPSLabels.Add("FPS");
                FPSSeries.Clear();
                FPSSeries.Add(new ColumnSeries
                {
                    Title = "FPS",
                    Values = new ChartValues<double> { metrics.FPS },
                    DataLabels = true,
                    LabelPoint = point => $"{point.Y:N0}"
                });

                // Update Frame Time Chart.
                FrameTimeLabels.Clear();
                FrameTimeLabels.Add("Frame Time");
                FrameTimeSeries.Clear();
                FrameTimeSeries.Add(new ColumnSeries
                {
                    Title = "Frame Time",
                    Values = new ChartValues<double> { metrics.FrameTime },
                    DataLabels = true,
                    LabelPoint = point => $"{point.Y:N0} ms"
                });

                // Update Active Players Chart.
                ActivePlayersLabels.Clear();
                ActivePlayersLabels.Add("Active Players");
                ActivePlayersSeries.Clear();
                ActivePlayersSeries.Add(new ColumnSeries
                {
                    Title = "Active Players",
                    Values = new ChartValues<double> { metrics.ActivePlayers },
                    DataLabels = true,
                    LabelPoint = point => $"{point.Y:N0}"
                });

                // Update Performance Summary.
                PerformanceSummary =
                    $"Total CPU: {metrics.OverallCpuUsage:N0}%\n" +
                    $"Memory Used: {metrics.MemoryUsedGB:N2} GB / {metrics.TotalMemoryGB:N2} GB (Usage: {metrics.MemoryUsagePercentage:N0}%)\n" +
                    $"Disk Read: {metrics.DiskReadMBps:N2} MB/s, Disk Write: {metrics.DiskWriteMBps:N2} MB/s, Disk Usage: {metrics.DiskUsagePercentage:N0}%\n" +
                    $"Network In: {metrics.NetworkInMBps:N2} MB/s, Network Out: {metrics.NetworkOutMBps:N2} MB/s\n" +
                    $"FPS: {metrics.FPS:N0}, Frame Time: {metrics.FrameTime:N0} ms\n" +
                    $"Active Players: {metrics.ActivePlayers}";
                PerformanceSummaryTextBlock.Text = PerformanceSummary;

                // Update Console Log Summary.
                ConsoleLogSummaryTextBlock.Text = ConsoleLogSummary;
            }
            catch (Exception ex)
            {
                Logger.Error("Exception in UpdateChartsAndSummary: {Message}", ex.Message);
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            _isConnected = true;
            Status = "Connected";
            StatusTextBlock.Text = Status;
            ConnectButton.IsEnabled = false;
            DisconnectButton.IsEnabled = true;

            // Open the Frontend Logs popup window.
            var logsWindow = new FrontendLogsWindow();
            logsWindow.Show();

            _pollTimer.Start();
            await Task.CompletedTask;
        }

        private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            _isConnected = false;
            Status = "Disconnected";
            StatusTextBlock.Text = Status;
            ConnectButton.IsEnabled = true;
            DisconnectButton.IsEnabled = false;
            _pollTimer.Stop();

            TotalCpuSeries.Clear();
            CpuSeries.Clear();
            MemorySeries.Clear();
            FPSSeries.Clear();
            FrameTimeSeries.Clear();
            ActivePlayersSeries.Clear();
            TotalCpuLabels.Clear();
            CpuLabels.Clear();
            MemoryLabels.Clear();
            FPSLabels.Clear();
            FrameTimeLabels.Clear();
            ActivePlayersLabels.Clear();

            PerformanceSummary = "No data";
            ConsoleLogSummary = "No console log entries parsed yet";
            PerformanceSummaryTextBlock.Text = PerformanceSummary;
            ConsoleLogSummaryTextBlock.Text = ConsoleLogSummary;

            await Task.CompletedTask;
        }

        private async void FetchRawDataButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var response = await _httpClient.GetAsync(ServerUrlTextBox.Text + "/api/data/rawdata");
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadAsStringAsync();
                    RawDataTextBox.Text = data;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error in FetchRawDataButton_Click: {Message}", ex.Message);
            }
        }

        private void StopRawDataButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement if needed.
        }

        private async void FetchBackendLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var response = await _httpClient.GetAsync(ServerUrlTextBox.Text + "/api/data/backendlogs");
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadAsStringAsync();
                    BackendLogsTextBlock.Text = data;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error in FetchBackendLogsButton_Click: {Message}", ex.Message);
            }
        }

        private void StopBackendLogsButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement if needed.
        }

        private async void FetchFrontendLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists("logs/frontend_log.json"))
                {
                    var data = await File.ReadAllTextAsync("logs/frontend_log.json");
                    FrontendLogsTextBlock.Text = data;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error in FetchFrontendLogsButton_Click: {Message}", ex.Message);
            }
        }

        private void StopFrontendLogsButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement if needed.
        }
    }
}
