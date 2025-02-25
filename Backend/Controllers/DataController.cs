using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArmaReforgerServerMonitor.Backend.Models;
using Serilog;

namespace ArmaReforgerServerMonitor.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataController : ControllerBase
    {
        private readonly DatabaseContext _dbContext;
        private readonly OSDataCollector _osDataCollector;

        public DataController(DatabaseContext dbContext, OSDataCollector osDataCollector)
        {
            _dbContext = dbContext;
            _osDataCollector = osDataCollector;
        }

        // GET /api/data/players
        // Returns players active in the last 5 minutes.
        [HttpGet("players")]
        public ActionResult<IEnumerable<Player>> GetActivePlayers()
        {
            var players = _dbContext.Players.Where(p => p.LastSeen >= DateTime.Now.AddMinutes(-5)).ToList();
            return Ok(players);
        }

        // GET /api/data/playerdatabase
        // Returns all players in the database.
        [HttpGet("playerdatabase")]
        public ActionResult<IEnumerable<Player>> GetPlayerDatabase()
        {
            var players = _dbContext.Players.ToList();
            return Ok(players);
        }

        // GET /api/data/rawdata
        // Returns the last 100 lines from the active console.log file.
        [HttpGet("rawdata")]
        public ActionResult<string> GetRawData()
        {
            var masterDir = ArmaReforgerServerMonitor.Backend.Setup.ConfigurationSetup.MasterLogsDirectory;
            var directories = Directory.GetDirectories(masterDir);
            var latestDir = directories.OrderByDescending(d => Directory.GetCreationTime(d)).FirstOrDefault();
            if (latestDir == null)
                return NotFound("No subdirectories found.");
            var logFile = Path.Combine(latestDir, "console.log");
            if (!System.IO.File.Exists(logFile))
                return NotFound("Log file not found.");
            var lines = System.IO.File.ReadAllLines(logFile);
            var last100 = lines.Skip(Math.Max(0, lines.Length - 100));
            return Ok(string.Join("\n", last100));
        }

        // GET /api/data/backendlogs
        // Returns backend logs stored in backend_logs.json.
        [HttpGet("backendlogs")]
        public ActionResult<IEnumerable<string>> GetBackendLogs()
        {
            var logFile = "backend_logs.json";
            if (!System.IO.File.Exists(logFile))
                return Ok(new string[] { "Logs: None so far" });
            var logs = System.IO.File.ReadAllLines(logFile);
            return Ok(logs);
        }

        // GET /api/data/consolelogstats
        // Returns a friendly summary of console log statistics based on the last parsed log line.
        [HttpGet("consolelogstats")]
        public ActionResult<string> GetConsoleLogStatistics()
        {
            try
            {
                string stats = Models.GameMetrics.LastConsoleLogLine;
                if (string.IsNullOrWhiteSpace(stats))
                    return Ok("No console log entries parsed yet.");
                return Ok(stats);
            }
            catch (Exception ex)
            {
                Log.Error("Error retrieving console log statistics: {Message}", ex.Message);
                return StatusCode(500, "Error retrieving console log statistics.");
            }
        }

        // GET /api/data/osmetrics
        // Returns operating system metrics, combined with realtime game metrics.
        [HttpGet("osmetrics")]
        public ActionResult<OSDataDTO> GetOSMetrics()
        {
            try
            {
                var metrics = _osDataCollector.GetOSMetrics();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                Log.Error("Error in GetOSMetrics: {Message}", ex.Message);
                return StatusCode(500, "Error collecting OS metrics.");
            }
        }
    }
}
