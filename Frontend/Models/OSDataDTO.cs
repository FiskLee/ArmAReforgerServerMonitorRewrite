using System.Collections.Generic;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    // This DTO must match exactly the backend OSDataDTO.
    public class OSDataDTO
    {
        public float OverallCpuUsage { get; set; }
        public Dictionary<string, float> PerCoreCpuUsage { get; set; } = new Dictionary<string, float>();
        public float MemoryUsagePercentage { get; set; }
        public float DiskReadMBps { get; set; }
        public float DiskWriteMBps { get; set; }
        public float DiskUsagePercentage { get; set; }
        public float NetworkInMBps { get; set; }
        public float NetworkOutMBps { get; set; }
        public float MemoryUsedGB { get; set; }
        public float TotalMemoryGB { get; set; }
        public float FPS { get; set; }
        public float FrameTime { get; set; }
        public int TotalLogEntries { get; set; }
        public int ActivePlayers { get; set; }
    }
}
