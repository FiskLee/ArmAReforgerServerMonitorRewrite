namespace ArmaReforgerServerMonitor.Frontend.Models
{
    public class DiskMetricsViewModel
    {
        public string DiskName { get; set; }
        public float DiskReadMBps { get; set; }
        public float DiskWriteMBps { get; set; }
        public float DiskUsagePercentage { get; set; }
    }
}
