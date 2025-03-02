namespace ArmaReforgerServerMonitor.Frontend.Models
{
    public class DiskMetricsViewModel
    {
        // Ensure DiskName is never null by providing a default value.
        public string DiskName { get; set; } = string.Empty;

        // Add any additional properties required for disk metrics.
        public float DiskReadMBps { get; set; }
        public float DiskWriteMBps { get; set; }
        public float DiskUsagePercentage { get; set; }
    }
}
