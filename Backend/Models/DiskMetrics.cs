namespace ArmaReforgerServerMonitor.Backend.Models
{
    /// <summary>
    /// Holds disk metrics for a single physical disk.
    /// </summary>
    public class DiskMetrics
    {
        /// <summary>
        /// Disk read throughput in MB/s.
        /// </summary>
        public float DiskReadMBps { get; set; }

        /// <summary>
        /// Disk write throughput in MB/s.
        /// </summary>
        public float DiskWriteMBps { get; set; }

        /// <summary>
        /// A heuristic disk usage percentage.
        /// </summary>
        public float DiskUsagePercentage { get; set; }
    }
}
