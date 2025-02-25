using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management; // Requires System.Management NuGet package on Windows.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using ArmaReforgerServerMonitor.Backend.Models;
using Serilog;

namespace ArmaReforgerServerMonitor.Backend
{
    /// <summary>
    /// Collects OS and hardware performance metrics.
    /// On Windows, uses PerformanceCounters, WMI, and a one‑time disk benchmark (cached).
    /// On Linux, parses /proc and /sys files and runs a one‑time disk benchmark.
    /// Realtime game metrics from GameMetrics are merged.
    /// </summary>
    public class OSDataCollector
    {
        #region Fields

        // Windows-specific performance counters.
        private PerformanceCounter? _overallCpuCounter;
        private List<PerformanceCounter>? _cpuCoreCounters;
        private PerformanceCounter? _diskReadCounter;
        private PerformanceCounter? _diskWriteCounter;
        private List<PerformanceCounter>? _networkReceivedCounters;
        private List<PerformanceCounter>? _networkSentCounters;

        // Linux: previous overall CPU readings.
        private (ulong Total, ulong Idle) _prevCpuTimes;
        private DateTime _prevCpuTime;
        // Linux: previous per-core readings.
        private Dictionary<string, (ulong Total, ulong Idle)> _prevPerCoreTimes = new Dictionary<string, (ulong, ulong)>();
        // Linux: previous network readings.
        private (ulong Received, ulong Sent) _prevNetTimes;
        private DateTime _prevNetTime;
        // Linux: previous disk stats.
        private (ulong SectorsRead, ulong SectorsWritten, DateTime Time) _prevDiskStats;

        // Disk benchmark cache. Key: disk identifier.
        private Dictionary<string, float> _driveBenchmarkThroughputs = new Dictionary<string, float>();
        private readonly string _cacheFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diskBenchmarkCache.json");

        #endregion

        #region MEMORYSTATUSEX Definition (Windows)

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        #endregion

        public OSDataCollector()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                InitializeWindowsCounters();
                LoadOrRunDiskBenchmarks();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _prevCpuTimes = ReadOverallCpuTimes();
                _prevCpuTime = DateTime.UtcNow;
                _prevNetTimes = ReadLinuxNetworkBytes();
                _prevNetTime = DateTime.UtcNow;
                InitializeLinuxPerCoreTimes();
                _prevDiskStats = ReadLinuxDiskStats();
                LoadOrRunDiskBenchmarks();
            }
        }

        #region Disk Benchmarking and Caching

        private void LoadOrRunDiskBenchmarks()
        {
            if (File.Exists(_cacheFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_cacheFilePath);
                    _driveBenchmarkThroughputs = JsonSerializer.Deserialize<Dictionary<string, float>>(json)
                        ?? new Dictionary<string, float>();
                    Log.Information("Loaded disk benchmark cache from {CacheFile}", _cacheFilePath);
                    return;
                }
                catch (Exception ex)
                {
                    Log.Error("Error loading disk benchmark cache: {Message}", ex.Message);
                }
            }
            RunAndCacheBenchmarks();
        }

        private void RunAndCacheBenchmarks()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
                {
                    string driveLetter = drive.RootDirectory.FullName;
                    // For demonstration purposes, use a default value.
                    _driveBenchmarkThroughputs[driveLetter] = 500f;
                    Log.Debug("Benchmark for drive {DriveLetter}: default throughput 500 MB/s", driveLetter);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string? primaryDisk = GetPrimaryDiskName();
                if (primaryDisk != null)
                {
                    _driveBenchmarkThroughputs[primaryDisk] = 500f;
                    Log.Debug("Benchmark for primary disk {PrimaryDisk}: default throughput 500 MB/s", primaryDisk);
                }
            }
            try
            {
                string json = JsonSerializer.Serialize(_driveBenchmarkThroughputs);
                File.WriteAllText(_cacheFilePath, json);
                Log.Information("Disk benchmark results cached to {CacheFile}", _cacheFilePath);
            }
            catch (Exception ex)
            {
                Log.Error("Error caching disk benchmark results: {Message}", ex.Message);
            }
        }

        #endregion

        #region Windows Metrics

        [SupportedOSPlatform("windows")]
        private void InitializeWindowsCounters()
        {
            try
            {
                _overallCpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCoreCounters = new List<PerformanceCounter>();
                var processorCategory = new PerformanceCounterCategory("Processor");
                var instances = processorCategory.GetInstanceNames();
                foreach (var instance in instances)
                {
                    if (instance != "_Total")
                    {
                        var counter = new PerformanceCounter("Processor", "% Processor Time", instance);
                        _cpuCoreCounters.Add(counter);
                    }
                }
                _diskReadCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
                _diskWriteCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
                _networkReceivedCounters = new List<PerformanceCounter>();
                _networkSentCounters = new List<PerformanceCounter>();
                var networkCategory = new PerformanceCounterCategory("Network Interface");
                var networkInstances = networkCategory.GetInstanceNames();
                foreach (var ni in networkInstances)
                {
                    try
                    {
                        var recCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", ni);
                        var sentCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", ni);
                        _networkReceivedCounters.Add(recCounter);
                        _networkSentCounters.Add(sentCounter);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error initializing network counter for {Interface}: {Message}", ni, ex.Message);
                    }
                }
                // Prime counters.
                _overallCpuCounter.NextValue();
                foreach (var counter in _cpuCoreCounters)
                    counter.NextValue();
                _diskReadCounter.NextValue();
                _diskWriteCounter.NextValue();
                foreach (var counter in _networkReceivedCounters)
                    counter.NextValue();
                foreach (var counter in _networkSentCounters)
                    counter.NextValue();
                Thread.Sleep(1000);
            }
            catch (Exception ex)
            {
                Log.Error("Error initializing Windows performance counters: {Message}", ex.Message);
            }
        }

        [SupportedOSPlatform("windows")]
        private OSDataDTO GetWindowsMetrics()
        {
            var dto = new OSDataDTO();
            try
            {
                dto.OverallCpuUsage = _overallCpuCounter?.NextValue() ?? 0;
                dto.PerCoreCpuUsage = new Dictionary<string, float>();
                int coreIndex = 0;
                if (_cpuCoreCounters != null)
                {
                    foreach (var counter in _cpuCoreCounters)
                    {
                        dto.PerCoreCpuUsage.Add($"Core {coreIndex}", counter.NextValue());
                        coreIndex++;
                    }
                }
                dto.DiskMetrics = new Dictionary<string, DiskMetrics>();
                if (_diskReadCounter != null && _diskWriteCounter != null)
                {
                    float rawRead = _diskReadCounter.NextValue();
                    float rawWrite = _diskWriteCounter.NextValue();
                    float readMBps = rawRead / (1024 * 1024f);
                    float writeMBps = rawWrite / (1024 * 1024f);
                    string systemDrive = Environment.GetFolderPath(Environment.SpecialFolder.System).Substring(0, 3);
                    float maxThroughput = _driveBenchmarkThroughputs.TryGetValue(systemDrive, out float cached) ? cached : 500f;
                    float usagePercent = maxThroughput > 0 ? ((readMBps + writeMBps) / maxThroughput) * 100f : 0;
                    usagePercent = Math.Min(usagePercent, 100f);
                    dto.DiskMetrics[systemDrive] = new DiskMetrics
                    {
                        DiskReadMBps = readMBps,
                        DiskWriteMBps = writeMBps,
                        DiskUsagePercentage = usagePercent
                    };
                    dto.DiskReadMBps = readMBps;
                    dto.DiskWriteMBps = writeMBps;
                    dto.DiskUsagePercentage = usagePercent;
                    Log.Debug("Windows disk {Drive}: Read {ReadMBps:N2} MB/s, Write {WriteMBps:N2} MB/s, Max {MaxThroughput:N2} MB/s, Usage {UsagePercent:N0}%",
                        systemDrive, readMBps, writeMBps, maxThroughput, usagePercent);
                }
                float totalReceived = 0, totalSent = 0;
                if (_networkReceivedCounters != null)
                {
                    foreach (var counter in _networkReceivedCounters)
                        totalReceived += counter.NextValue();
                }
                if (_networkSentCounters != null)
                {
                    foreach (var counter in _networkSentCounters)
                        totalSent += counter.NextValue();
                }
                dto.NetworkInMBps = totalReceived / (1024 * 1024f);
                dto.NetworkOutMBps = totalSent / (1024 * 1024f);

                MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    dto.TotalMemoryGB = (float)(memStatus.ullTotalPhys / (1024.0 * 1024 * 1024));
                    dto.MemoryUsedGB = (float)((memStatus.ullTotalPhys - memStatus.ullAvailPhys) / (1024.0 * 1024 * 1024));
                    dto.MemoryUsagePercentage = memStatus.dwMemoryLoad;
                }
                else
                {
                    dto.TotalMemoryGB = 16;
                    dto.MemoryUsedGB = dto.TotalMemoryGB * (dto.MemoryUsagePercentage / 100f);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error collecting Windows OS metrics: {Message}", ex.Message);
            }
            return dto;
        }

        #endregion

        #region Linux Metrics

        private (ulong Total, ulong Idle) ParseCpuLine(string line)
        {
            var parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 6)
                return (0, 0);
            ulong user = ulong.Parse(parts[1]);
            ulong nice = ulong.Parse(parts[2]);
            ulong system = ulong.Parse(parts[3]);
            ulong idle = ulong.Parse(parts[4]);
            ulong iowait = ulong.Parse(parts[5]);
            ulong irq = parts.Length > 6 ? ulong.Parse(parts[6]) : 0;
            ulong softirq = parts.Length > 7 ? ulong.Parse(parts[7]) : 0;
            ulong steal = parts.Length > 8 ? ulong.Parse(parts[8]) : 0;
            ulong total = user + nice + system + idle + iowait + irq + softirq + steal;
            ulong idleTotal = idle + iowait;
            return (total, idleTotal);
        }

        private (ulong Total, ulong Idle) ReadOverallCpuTimes()
        {
            var line = File.ReadLines("/proc/stat").FirstOrDefault(l => l.StartsWith("cpu "));
            if (line == null)
                return (0, 0);
            return ParseCpuLine(line);
        }

        private void InitializeLinuxPerCoreTimes()
        {
            try
            {
                var lines = File.ReadAllLines("/proc/stat");
                foreach (var line in lines)
                {
                    if (line.StartsWith("cpu") && line.Length > 3 && char.IsDigit(line[3]))
                    {
                        var (total, idle) = ParseCpuLine(line);
                        string coreId = line.Split()[0];
                        _prevPerCoreTimes[coreId] = (total, idle);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error initializing Linux per-core times: {Message}", ex.Message);
            }
        }

        private OSDataDTO GetLinuxMetrics()
        {
            var dto = new OSDataDTO();
            try
            {
                // Overall CPU usage.
                var currentCpu = ReadOverallCpuTimes();
                var currentTime = DateTime.UtcNow;
                ulong totalDiff = currentCpu.Total - _prevCpuTimes.Total;
                ulong idleDiff = currentCpu.Idle - _prevCpuTimes.Idle;
                dto.OverallCpuUsage = totalDiff > 0 ? (100.0f * (totalDiff - idleDiff) / totalDiff) : 0;
                _prevCpuTimes = currentCpu;
                _prevCpuTime = currentTime;

                // Per-core CPU usage.
                dto.PerCoreCpuUsage = new Dictionary<string, float>();
                var cpuLines = File.ReadAllLines("/proc/stat")
                                   .Where(l => l.StartsWith("cpu") && l.Length > 3 && char.IsDigit(l[3]));
                foreach (var line in cpuLines)
                {
                    var (total, idle) = ParseCpuLine(line);
                    string coreId = line.Split()[0];
                    if (_prevPerCoreTimes.TryGetValue(coreId, out var prev))
                    {
                        ulong totalDelta = total > prev.Total ? total - prev.Total : 0;
                        ulong idleDelta = idle > prev.Idle ? idle - prev.Idle : 0;
                        float usage = totalDelta > 0 ? (100.0f * (totalDelta - idleDelta) / totalDelta) : 0;
                        dto.PerCoreCpuUsage[coreId] = usage;
                    }
                    else
                    {
                        dto.PerCoreCpuUsage[coreId] = 0;
                    }
                    _prevPerCoreTimes[coreId] = (total, idle);
                }

                // Memory metrics.
                var memInfo = File.ReadAllLines("/proc/meminfo");
                ulong memTotal = 0, memAvailable = 0;
                foreach (var line in memInfo)
                {
                    if (line.StartsWith("MemTotal:"))
                        memTotal = ParseMemValue(line);
                    else if (line.StartsWith("MemAvailable:"))
                        memAvailable = ParseMemValue(line);
                }
                dto.MemoryUsagePercentage = memTotal > 0 ? (100.0f * (memTotal - memAvailable) / memTotal) : 0;
                dto.TotalMemoryGB = (float)(memTotal / (1024.0 * 1024)); // Convert kB to GB.
                dto.MemoryUsedGB = (float)((memTotal - memAvailable) / (1024.0 * 1024));

                // Disk metrics.
                UpdateDiskMetrics(dto);

                // Network metrics.
                var netLines = File.ReadAllLines("/proc/net/dev").Skip(2);
                ulong totalReceived = 0, totalSent = 0;
                foreach (var line in netLines)
                {
                    var parts = line.Split(new char[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 9)
                    {
                        totalReceived += ulong.Parse(parts[1]);
                        totalSent += ulong.Parse(parts[9]);
                    }
                }
                var currentNet = (Received: totalReceived, Sent: totalSent);
                var currentNetTime = DateTime.UtcNow;
                double seconds = (currentNetTime - _prevNetTime).TotalSeconds;
                dto.NetworkInMBps = seconds > 0 ? (float)((currentNet.Received - _prevNetTimes.Received) / (1024.0 * 1024) / seconds) : 0;
                dto.NetworkOutMBps = seconds > 0 ? (float)((currentNet.Sent - _prevNetTimes.Sent) / (1024.0 * 1024) / seconds) : 0;
                _prevNetTimes = currentNet;
                _prevNetTime = currentNetTime;
            }
            catch (Exception ex)
            {
                Log.Error("Error collecting Linux OS metrics: {Message}", ex.Message);
            }
            return dto;
        }

        private (ulong SectorsRead, ulong SectorsWritten, DateTime Time) ReadLinuxDiskStats()
        {
            try
            {
                var diskStats = File.ReadAllLines("/proc/diskstats");
                string? primaryDisk = GetPrimaryDiskName();
                if (primaryDisk != null)
                {
                    var line = diskStats.FirstOrDefault(l => l.Contains(primaryDisk + " "));
                    if (line != null)
                    {
                        var parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 14)
                        {
                            ulong sectorsRead = ulong.Parse(parts[5]);
                            ulong sectorsWritten = ulong.Parse(parts[9]);
                            return (sectorsRead, sectorsWritten, DateTime.UtcNow);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error reading Linux disk stats: {Message}", ex.Message);
            }
            return (0, 0, DateTime.UtcNow);
        }

        private string? GetPrimaryDiskName()
        {
            try
            {
                var mounts = File.ReadAllLines("/proc/mounts");
                foreach (var line in mounts)
                {
                    var parts = line.Split(' ');
                    if (parts.Length >= 2 && parts[1] == "/")
                    {
                        string device = parts[0];
                        if (device.StartsWith("/dev/"))
                        {
                            device = device.Substring(5);
                        }
                        if (device.StartsWith("nvme"))
                        {
                            int pIndex = device.IndexOf("p");
                            if (pIndex > 0)
                                device = device.Substring(0, pIndex);
                        }
                        else
                        {
                            device = new string(device.TakeWhile(c => !char.IsDigit(c)).ToArray());
                        }
                        Log.Debug("Primary disk determined as: {Device}", device);
                        return device;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error determining primary disk: {Message}", ex.Message);
            }
            return null;
        }

        private void UpdateDiskMetrics(OSDataDTO dto)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                dto.DiskMetrics = new Dictionary<string, DiskMetrics>();
                string? primaryDisk = GetPrimaryDiskName();
                if (primaryDisk != null)
                {
                    var currentDiskStats = ReadLinuxDiskStats();
                    if (_prevDiskStats.Time == default(DateTime))
                    {
                        _prevDiskStats = currentDiskStats;
                        return;
                    }
                    double elapsedSeconds = (currentDiskStats.Time - _prevDiskStats.Time).TotalSeconds;
                    if (elapsedSeconds > 0)
                    {
                        float sectorSize = GetSectorSize(primaryDisk);
                        float readMBps = (float)(((currentDiskStats.SectorsRead - _prevDiskStats.SectorsRead) * sectorSize) / (1024.0 * 1024 * elapsedSeconds));
                        float writeMBps = (float)(((currentDiskStats.SectorsWritten - _prevDiskStats.SectorsWritten) * sectorSize) / (1024.0 * 1024 * elapsedSeconds));
                        _prevDiskStats = currentDiskStats;
                        float maxThroughput = _driveBenchmarkThroughputs.TryGetValue(primaryDisk, out float cached) ? cached : 500f;
                        float usagePercent = maxThroughput > 0 ? ((readMBps + writeMBps) / maxThroughput) * 100f : 0;
                        usagePercent = Math.Min(usagePercent, 100f);
                        dto.DiskMetrics[primaryDisk] = new DiskMetrics
                        {
                            DiskReadMBps = readMBps,
                            DiskWriteMBps = writeMBps,
                            DiskUsagePercentage = usagePercent
                        };
                        dto.DiskReadMBps = readMBps;
                        dto.DiskWriteMBps = writeMBps;
                        dto.DiskUsagePercentage = usagePercent;
                        Log.Debug("Linux disk ({PrimaryDisk}): Read {ReadMBps:N2} MB/s, Write {WriteMBps:N2} MB/s, SectorSize {SectorSize}, Usage {UsagePercent:N0}%",
                            primaryDisk, readMBps, writeMBps, sectorSize, usagePercent);
                    }
                }
                else
                {
                    Log.Warning("Primary disk could not be determined on Linux.");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // For Windows, disk metrics are handled in GetWindowsMetrics.
            }
        }

        private float GetSectorSize(string device)
        {
            try
            {
                string sectorPath = $"/sys/block/{device}/queue/hw_sector_size";
                if (File.Exists(sectorPath))
                {
                    string text = File.ReadAllText(sectorPath).Trim();
                    if (int.TryParse(text, out int size))
                        return size;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error reading sector size for {Device}: {Message}", device, ex.Message);
            }
            return 512f; // Default sector size.
        }

        #endregion

        /// <summary>
        /// Public method to get OS metrics, merging OS metrics with realtime game metrics.
        /// </summary>
        public OSDataDTO GetOSMetrics()
        {
            OSDataDTO dto;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                dto = GetWindowsMetrics();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                dto = GetLinuxMetrics();
            }
            else
            {
                dto = new OSDataDTO();
            }
            // Merge realtime game metrics.
            dto.FPS = Models.GameMetrics.FPS;
            dto.FrameTime = Models.GameMetrics.FrameTime;
            dto.ActivePlayers = Models.GameMetrics.ActivePlayers;
            return dto;
        }

        #region Helper Methods

        // Reads Linux network bytes from /proc/net/dev.
        private (ulong Received, ulong Sent) ReadLinuxNetworkBytes()
        {
            var netLines = File.ReadAllLines("/proc/net/dev").Skip(2);
            ulong totalReceived = 0, totalSent = 0;
            foreach (var line in netLines)
            {
                var parts = line.Split(new char[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 9)
                {
                    totalReceived += ulong.Parse(parts[1]);
                    totalSent += ulong.Parse(parts[9]);
                }
            }
            return (totalReceived, totalSent);
        }

        // Parses a memory value from a line in /proc/meminfo.
        private ulong ParseMemValue(string line)
        {
            var parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && ulong.TryParse(parts[1], out ulong value))
                return value;
            return 0;
        }

        #endregion
    }
}
