using System;

namespace ArmaReforgerServerMonitor.Backend.Models
{
    // Represents a player parsed from server logs.
    public class Player
    {
        public int Id { get; set; }
        // Initialize with an empty string so that it is never null.
        public string Name { get; set; } = string.Empty;
        public string Identity { get; set; } = string.Empty;
        public int PlayerNumber { get; set; }
        public string IPAddress { get; set; } = string.Empty;
        public string GUID { get; set; } = string.Empty;
        public string BEGUID { get; set; } = string.Empty;
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public int TotalSessions { get; set; }
        public TimeSpan TotalSessionDuration { get; set; }
    }
}
