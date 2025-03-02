using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Timer = System.Timers.Timer; // Use System.Timers.Timer to avoid ambiguity
using Microsoft.Extensions.Logging;
using Serilog;

namespace ArmaReforgerServerMonitor.Backend
{
    /// <summary>
    /// Processes console.log files by performing a full scan and realtime (tail) monitoring
    /// using a persistent FileStream. It parses each log line to extract game metrics such as FPS,
    /// frame time (avg, min, max), Player count, etc. The parsed values update the SharedGameMetrics.
    /// </summary>
    public class LogProcessor : IDisposable
    {
        private readonly ILogger<LogProcessor> _logger;
        private readonly DatabaseContext _dbContext;
        private readonly Timer _pollTimer;
        private FileStream? _logStream;
        private StreamReader? _logReader;
        private string _currentLogFile = string.Empty;
        private readonly object _streamLock = new object();
        private bool _disposed = false;
        // Last read offset for incremental parsing.
        private long _lastOffset = 0;

        // Polling interval in milliseconds (2 seconds).
        private const int PollIntervalMs = 2000;
        // Maximum number of lines per batch.
        private const int BatchSize = 100;

        // Compiled Regex to match log lines with performance metrics.
        // Example log line:
        // "11:50:57.438   DEFAULT      : FPS: 60.1, frame time (avg: 16.6 ms, min: 15.0 ms, max: 19.2 ms), Mem: 4896197 kB, Player: 1, AI: 1156, AIChar: 849, Veh: 0 (5), ..."
        private readonly Regex _logRegex = new Regex(
            @"FPS:\s*(?<fps>\d+(\.\d+)?),\s*frame time\s*\(avg:\s*(?<avg>\d+(\.\d+)?)\s*ms,\s*min:\s*(?<min>\d+(\.\d+)?)\s*ms,\s*max:\s*(?<max>\d+(\.\d+)?)\s*ms\).*Player:\s*(?<players>\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public LogProcessor(ILogger<LogProcessor> logger, DatabaseContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
            _pollTimer = new Timer(PollIntervalMs) { AutoReset = true };
            _pollTimer.Elapsed += async (s, e) => await ProcessNewContentAsync();
        }

        /// <summary>
        /// Starts log processing by performing a full scan (if configured) and initializing realtime monitoring.
        /// </summary>
        public void Start()
        {
            if (Setup.ConfigurationSetup.FullScanOption)
            {
                FullScan();
            }
            InitializePersistentStream();
            _pollTimer.Start();
        }

        /// <summary>
        /// Performs a full scan of all console.log files.
        /// </summary>
        private void FullScan()
        {
            try
            {
                _logger.LogInformation("Starting full scan of logs...");
                var logFiles = Directory.GetFiles(Setup.ConfigurationSetup.MasterLogsDirectory, "console.log", SearchOption.AllDirectories);
                foreach (var file in logFiles)
                {
                    ProcessLogFile(file);
                }
                _logger.LogInformation("Full scan completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during full scan: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Processes an entire log file line-by-line.
        /// </summary>
        private void ProcessLogFile(string filePath)
        {
            try
            {
                var lines = File.ReadAllLines(filePath);
                foreach (var line in lines)
                {
                    if (_logRegex.IsMatch(line))
                    {
                        ParseLogLine(line);
                    }
                    else
                    {
                        _logger.LogDebug("Discarded line (pattern mismatch): {Line}", line);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error processing log file '{File}': {Message}", filePath, ex.Message);
            }
        }

        /// <summary>
        /// Initializes a persistent FileStream and StreamReader for realtime monitoring.
        /// Seeks to the last known offset to avoid reprocessing old data.
        /// </summary>
        private void InitializePersistentStream()
        {
            lock (_streamLock)
            {
                try
                {
                    var directories = Directory.GetDirectories(Setup.ConfigurationSetup.MasterLogsDirectory);
                    var latestDir = directories.OrderByDescending(d => Directory.GetCreationTime(d)).FirstOrDefault();
                    if (latestDir == null)
                    {
                        _logger.LogWarning("No subdirectories found for realtime monitoring.");
                        return;
                    }
                    string logFile = Path.Combine(latestDir, "console.log");
                    if (!File.Exists(logFile))
                    {
                        _logger.LogWarning("No console.log file found in directory: {Dir}", latestDir);
                        return;
                    }
                    _logReader?.Dispose();
                    _logStream?.Dispose();
                    _currentLogFile = logFile;
                    _logStream = new FileStream(
                        _currentLogFile,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete,
                        bufferSize: 4096,
                        FileOptions.SequentialScan);
                    _logReader = new StreamReader(_logStream);
                    // Seek to the last processed offset.
                    _logStream.Seek(_lastOffset, SeekOrigin.Begin);
                    _logger.LogInformation("Persistent stream initialized on file '{File}' at offset {Offset}", _currentLogFile, _lastOffset);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error initializing persistent stream: {Message}", ex.Message);
                }
            }
        }

        /// <summary>
        /// Asynchronously reads new log lines in batches and processes them.
        /// </summary>
        private async Task ProcessNewContentAsync()
        {
            List<string> batch = new List<string>();
            lock (_streamLock)
            {
                if (_logStream == null || _logReader == null)
                {
                    _logger.LogWarning("Persistent stream not initialized. Reinitializing...");
                    InitializePersistentStream();
                    return;
                }
            }

            try
            {
                while (!_logReader.EndOfStream)
                {
                    string? line = await _logReader.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        lock (_streamLock)
                        {
                            _lastOffset = _logStream.Position;
                        }
                        if (_logRegex.IsMatch(line))
                        {
                            batch.Add(line);
                        }
                        else
                        {
                            _logger.LogDebug("Discarded line (pattern mismatch): {Line}", line);
                        }
                    }
                    if (batch.Count >= BatchSize)
                    {
                        ProcessBatch(batch);
                        batch.Clear();
                    }
                }
                if (batch.Any())
                {
                    ProcessBatch(batch);
                }
                _logger.LogInformation("Finished processing new content. Current offset: {Offset}", _lastOffset);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error while reading new content: {Message}. Reinitializing persistent stream.", ex.Message);
                InitializePersistentStream();
            }
            await Task.Delay(250);
        }

        /// <summary>
        /// Processes a batch of log lines.
        /// </summary>
        private void ProcessBatch(List<string> batch)
        {
            _logger.LogDebug("Processing batch of {Count} lines", batch.Count);
            foreach (var line in batch)
            {
                try
                {
                    ParseLogLine(line);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error processing log line: {Message}", ex.Message);
                }
            }
        }

        /// <summary>
        /// Parses a single log line and updates SharedGameMetrics.
        /// </summary>
        private void ParseLogLine(string line)
        {
            try
            {
                _logger.LogDebug("Parsing log line: {Line}", line);
                var match = _logRegex.Match(line);
                if (match.Success)
                {
                    if (float.TryParse(match.Groups["fps"].Value, out float fps))
                        SharedGameMetrics.FPS = fps;
                    if (float.TryParse(match.Groups["avg"].Value, out float avg))
                        SharedGameMetrics.FrameTimeAvg = avg;
                    if (float.TryParse(match.Groups["min"].Value, out float min))
                        SharedGameMetrics.FrameTimeMin = min;
                    if (float.TryParse(match.Groups["max"].Value, out float max))
                        SharedGameMetrics.FrameTimeMax = max;
                    if (int.TryParse(match.Groups["players"].Value, out int players))
                        SharedGameMetrics.Players = players;
                    SharedGameMetrics.LastConsoleLogLine = line;

                    string friendly = $"FPS: {SharedGameMetrics.FPS}, Frame Time (avg: {SharedGameMetrics.FrameTimeAvg} ms, min: {SharedGameMetrics.FrameTimeMin} ms, max: {SharedGameMetrics.FrameTimeMax} ms), " +
                                      $"Players: {SharedGameMetrics.Players}";
                    _logger.LogInformation("Parsed log line: {FriendlySummary}", friendly);
                }
                else
                {
                    _logger.LogDebug("Log line did not match expected format: {Line}", line);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error parsing log line: {Message}. Line: {Line}", ex.Message, line);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _pollTimer.Stop();
            _pollTimer.Dispose();
            _logReader?.Dispose();
            _logStream?.Dispose();
        }
    }

    /// <summary>
    /// SharedGameMetrics holds parsed game performance metrics.
    /// </summary>
    public static class SharedGameMetrics
    {
        public static float FPS { get; set; }
        public static float FrameTimeAvg { get; set; }
        public static float FrameTimeMin { get; set; }
        public static float FrameTimeMax { get; set; }
        public static int Players { get; set; }
        public static string LastConsoleLogLine { get; set; } = string.Empty;
    }
}
