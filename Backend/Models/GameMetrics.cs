namespace ArmaReforgerServerMonitor.Backend.Models
{
    /// <summary>
    /// Holds real-time game performance metrics parsed from the console log.
    /// </summary>
    public static class GameMetrics
    {
        public static float FPS { get; set; }
        public static float FrameTimeAvg { get; set; }
        public static float FrameTimeMin { get; set; }
        public static float FrameTimeMax { get; set; }
        public static int Players { get; set; }
        public static int AI { get; set; }
        public static int AIChar { get; set; }
        public static int Veh { get; set; }
        public static string LastConsoleLogLine { get; set; } = string.Empty;

        // For convenience, a combined frame time value (here, using average)
        public static float FrameTime => FrameTimeAvg;
        // For convenience, treat Players as the active player count.
        public static int ActivePlayers => Players;

        /// <summary>
        /// Returns a human-readable summary of the current game metrics.
        /// </summary>
        public static string GetSummary()
        {
            return $"FPS: {FPS}, Frame Time (avg: {FrameTimeAvg} ms, min: {FrameTimeMin} ms, max: {FrameTimeMax} ms), " +
                   $"Players: {Players}, AI: {AI}, AIChar: {AIChar}, Veh: {Veh}";
        }
    }
}
