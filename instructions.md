# Backend Installation on Linux

## Prerequisites
- Install the .NET 6.0+ Runtime/SDK.
- Ensure the user running the package has appropriate permissions to read server log directories.

## Deploy the Backend
1. Copy the pre‑built self‑contained package (targeting linux-x64) to your Linux server.
2. Unpack the package into a chosen folder (e.g., `/opt/ArmaMonitor`).

## Configuration Setup
On first run, the backend prompts you to enter:
- **The Master Logs Directory** (for example: `/home/admin/arma-reforger/1874900/AReforgerMaster/logs/`). You must enter a valid directory – no default is assumed.
- **An admin username and password** that the frontend will use to connect.

These settings are saved in a JSON configuration file for subsequent startups.

## Run as a Service with systemd
Create a systemd service file, e.g., `/etc/systemd/system/arma-monitor.service`, with content similar to:

```ini
[Unit]
Description=Arma Reforger Server Monitor Backend
After=network.target

[Service]
WorkingDirectory=/opt/ArmaMonitor
ExecStart=/opt/ArmaMonitor/ArmaReforgerServerMonitor.Backend
Restart=on-failure
User=youruser
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

### Reload systemd:
```bash
sudo systemctl daemon-reload
```

### Enable and start the service:
```bash
sudo systemctl enable arma-monitor.service
sudo systemctl start arma-monitor.service
```

---

## Backend Installation on Windows

### Prerequisites
- Ensure that the target machine has the necessary Windows version (e.g. Windows Server 2016 or later).

### Deploy the Backend
1. Copy the pre‑built self‑contained package (targeting win-x64) to your Windows Server (e.g., extract it to `C:\ArmaMonitor`).

### Configuration Setup
Run the backend executable. On first launch, you’ll be prompted for:
- **The Master Logs Directory** (for example: `C:\Program Files (x86)\Steam\steamapps\common\Arma Reforger`).
- **An admin username and password**.

These values are stored in a configuration JSON file for future startups.

### Run on Startup
**Option A – Task Scheduler:**
1. Open Task Scheduler and select Create Task.
2. Under General, give the task a name (e.g., “Arma Monitor Backend”).
3. In Triggers, add a trigger to run At Startup.
4. In Actions, add a new action to Start a program, pointing to your backend executable (e.g., `C:\ArmaMonitor\ArmaReforgerServerMonitor.Backend.exe`).
5. Save and test the task by restarting the machine.

**Option B – NSSM (Non-Sucking Service Manager):**
1. Download and install NSSM from nssm.cc.
2. Open an elevated command prompt and run:
   ```bash
   nssm install "ArmaMonitorBackend" "C:\ArmaMonitor\ArmaReforgerServerMonitor.Backend.exe"
   nssm start "ArmaMonitorBackend"
   ```

---

## Frontend Installation on Windows

### Prerequisites
- Windows 10 or later.

### Deploy the Frontend
1. Copy the pre‑built self‑contained package of the WPF frontend to the target system.
2. Ensure that any required configuration (backend URL, credentials) is set up.

### Running the Frontend
1. Launch the frontend executable.
2. In the connection panel, enter the backend URL (e.g., `http://<server-ip>:5000`), along with the username and password you set during backend configuration.
3. The dashboard will display real‑time charts (CPU, memory, disk, network, FPS, frame time, active players) and detailed server performance summaries.

---

## Detailed Summary of Changes & Features

### Backend
- **Log Processing:**
  - Performs a full scan of console log files on first run.
  - Monitors the active console log file in real‑time using a persistent file stream.
  - Parses each log line for game metrics (FPS, frame time avg/min/max, player counts, etc.) and updates a shared GameMetrics class.

- **OS Metrics Collection:**
  - **Windows:** Uses PerformanceCounters, WMI, and native APIs (GlobalMemoryStatusEx) to collect CPU, memory, disk, and network data.
  - **Linux:** Parses `/proc/stat`, `/proc/meminfo`, `/proc/net/dev`, and `/proc/diskstats` (plus `/sys/block/` for sector size) to compute similar metrics.

- **Disk Benchmarking & Caching:**
  - Benchmarks each physical disk (or queries hardware using native APIs) on first run to estimate maximum throughput.
  - Caches benchmark results in a JSON file to avoid re‑benchmarking on every startup.

- **Configuration Setup:**
  - Prompts for a valid master logs directory, username, and password on first run.
  - Saves settings in a JSON configuration file for future startups.

- **REST API Endpoints:**
  - Provides endpoints to fetch OS metrics, player data, raw log data, console log statistics, and backend logs.

### Frontend
- **Real‑Time Dashboard:**
  - Displays charts for total CPU, per‑core CPU, memory usage, FPS, frame time, and active players using LiveCharts.
  - Shows a detailed server performance summary including disk read/write, disk usage, network throughput, and game metrics.

- **Connection Management:**
  - Continuously polls the backend every 2 seconds.
  - Detects and handles connection loss by resetting the UI and prompting the user to reconnect.
  - Includes a popup logs window to display frontend logs for troubleshooting.

- **User Configuration:**
  - Requires user credentials and backend URL to connect.
  - Uses these credentials to authenticate with the backend.

---

## How the Apps Work Together
The backend continuously gathers OS and game metrics, processes console logs, benchmarks disk throughput (caching the results), and exposes the data via REST API endpoints. The frontend polls these endpoints and updates its dashboard in real‑time, providing a comprehensive overview of server performance. Together, they offer a complete, production‑ready solution for monitoring Arma Reforger servers.

---

## Key Highlights
- **Accurate Metrics Collection:**
  - Uses native APIs (PerformanceCounters, WMI, /proc parsing) and dynamic disk benchmarking (with caching) for precise measurements.

- **Real‑Time Monitoring:**
  - Continuously updates server performance data and game metrics via a REST API.

- **Flexible Deployment:**
  - Backend runs on both Windows and Linux (with systemd on Linux and Task Scheduler/NSSM on Windows). The frontend is a Windows WPF application.

- **Production‑Ready:**
  - Robust error handling, persistent configuration, and detailed logging via Serilog ensure the system is reliable and easy to troubleshoot.
