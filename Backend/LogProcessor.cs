using System;
using System.IO;
using System.Linq;
using System.Timers;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ArmaReforgerServerMonitor.Backend.Models;
using Serilog;
using Timer = System.Timers.Timer; // Use System.Timers.Timer

namespace ArmaReforgerServerMonitor.Backend
{
    /// <summary>
    /// Processes console.log files by performing a full scan and realtime (tail) monitoring
    /// using a persistent FileStream. It parses each log line to extract game metrics such as FPS,
    /// frame time (avg, min, max), Player, AI, AIChar, and Veh. The parsed values are used to update
    /// the shared GameMetrics class and a friendly summary of the latest log entry.
    /// </summary>
    public class LogProcessor : IDisposable
    {
        private readonly ILogger<LogProcessor> _logger;
        private readonly DatabaseContext _dbContext;
        private Timer _pollTimer;
        private FileStream? _logStream;
        private StreamReader? _logReader;
        private string _currentLogFile = string.Empty;
        private readonly object _streamLock = new object();
        private bool _disposed = false;

        // Polling interval: 2000 ms (2 seconds)
        private const int PollIntervalMs = 2000;

        // Regex pattern to extract metrics.
        // Sample log line:
        // "23:19:30.324   DEFAULT      : FPS: 60.0, frame time (avg: 16.7 ms, min: 12.1 ms, max: 20.1 ms), Mem: 7570880 kB, Player: 32, AI: 109, AIChar: 56, Veh: 8 (15), ..."
        private readonly Regex _logRegex = new Regex(
            @"FPS:\s*(?<fps>\d+(\.\d+)?),\s*frame time\s*\(avg:\s*(?<avg>\d+(\.\d+)?)\s*ms,\s*min:\s*(?<min>\d+(\.\d+)?)\s*ms,\s*max:\s*(?<max>\d+(\.\d+)?)\s*ms\).*Player:\s*(?<players>\d+),\s*AI:\s*(?<ai>\d+),\s*AIChar:\s*(?<aichar>\d+),\s*Veh:\s*(?<veh>\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public LogProcessor(ILogger<LogProcessor> logger, DatabaseContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
            _pollTimer = new Timer(PollIntervalMs) { AutoReset = true };
            _pollTimer.Elapsed += PollTimer_Elapsed;
        }

        /// <summary>
        /// Starts log processing. If full scan is configured, performs a full scan first,
        /// then initializes realtime monitoring.
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
        /// Processes an entire log file.
        /// </summary>
        private void ProcessLogFile(string filePath)
        {
            try
            {
                var lines = File.ReadAllLines(filePath);
                foreach (var line in lines)
                {
                    ParseLogLine(line);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error processing log file '{File}': {Message}", filePath, ex.Message);
            }
        }

        /// <summary>
        /// Initializes a persistent stream and reader for realtime monitoring.
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

                    // Dispose any existing stream/reader.
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
                    // Seek to the end of file so we only get new lines.
                    _logStream.Seek(0, SeekOrigin.End);
                    _logger.LogInformation("Persistent stream initialized on file '{File}' at position {Pos}", _currentLogFile, _logStream.Position);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error initializing persistent stream: {Message}", ex.Message);
                }
            }
        }

        /// <summary>
        /// Timer event handler to poll for new log content.
        /// </summary>
        private void PollTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            ProcessNewContent();
        }

        /// <summary>
        /// Reads new log lines from the persistent stream.
        /// </summary>
        private void ProcessNewContent()
        {
            lock (_streamLock)
            {
                if (_logStream == null || _logReader == null)
                {
                    _logger.LogWarning("Persistent stream not initialized. Reinitializing...");
                    InitializePersistentStream();
                    return;
                }

                try
                {
                    while (!_logReader.EndOfStream)
                    {
                        string? line = _logReader.ReadLine();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            _logger.LogDebug("Read log line: {Line}", line);
                            ParseLogLine(line);
                        }
                    }
                    _logger.LogInformation("Finished processing new content. Current stream position: {Pos}", _logStream.Position);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error while reading new content: {Message}. Reinitializing persistent stream.", ex.Message);
                    InitializePersistentStream();
                }
            }
        }

        /// <summary>
        /// Parses a log line using a regex pattern and updates GameMetrics.
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
                        GameMetrics.FPS = fps;
                    if (float.TryParse(match.Groups["avg"].Value, out float avg))
                        GameMetrics.FrameTimeAvg = avg;
                    if (float.TryParse(match.Groups["min"].Value, out float min))
                        GameMetrics.FrameTimeMin = min;
                    if (float.TryParse(match.Groups["max"].Value, out float max))
                        GameMetrics.FrameTimeMax = max;
                    if (int.TryParse(match.Groups["players"].Value, out int players))
                        GameMetrics.Players = players;
                    if (int.TryParse(match.Groups["ai"].Value, out int ai))
                        GameMetrics.AI = ai;
                    if (int.TryParse(match.Groups["aichar"].Value, out int aichar))
                        GameMetrics.AIChar = aichar;
                    if (int.TryParse(match.Groups["veh"].Value, out int veh))
                        GameMetrics.Veh = veh;

                    // Update the last parsed log line for console statistics.
                    GameMetrics.LastConsoleLogLine = line;

                    string friendly = $"FPS: {GameMetrics.FPS}, Frame Time (avg: {GameMetrics.FrameTimeAvg} ms, min: {GameMetrics.FrameTimeMin} ms, max: {GameMetrics.FrameTimeMax} ms), " +
                                      $"Player: {GameMetrics.Players}, AI: {GameMetrics.AI}, AIChar: {GameMetrics.AIChar}, Veh: {GameMetrics.Veh}";
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
            if (_disposed) return;
            _disposed = true;
            _pollTimer?.Stop();
            _pollTimer?.Dispose();
            _logReader?.Dispose();
            _logStream?.Dispose();
        }
    }
}
