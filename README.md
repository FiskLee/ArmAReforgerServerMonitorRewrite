# ArmAReforgerServerMonitorRewrite
this is a working rewrite of the original project  https://github.com/FiskLee/ArmAReforgerServerMonitor

This project is a major work in progress. I am actively making daily updates that include fixes, enhancements, and the rollout of new functionalities.
Support me here:
https://ko-fi.com/armagamingworld

Disclaimer: this is made by ChatGPT

 Arma Reforger Server Monitor ARSM is a two-part solution,a backend and a frontend designed to monitor and troubleshoot a running Arma Reforger server in real time. It gives you live FPS, frame times, memory usage, CPU usage (total + per-core), player info, and more—while writing verbose logs for easy debugging in production.



# Installing the Backend on Linux

## Download the Installer Script
Open a terminal and run the following command to download the installation script:

```bash
wget https://raw.githubusercontent.com/FiskLee/ArmAReforgerServerMonitorRewrite/main/installscript.sh
```

## Make the Script Executable
Change the permissions so that the script can be executed:

```bash
chmod +x installscript.sh
```

## Run the Script as Root
Since the script installs prerequisites and sets up a systemd service, you must run it with root privileges:

```bash
sudo ./installscript.sh
```

## Verify the Installation
Once the script completes, check that the service is running:

```bash
systemctl status armareforger-backend.service
```

You can also view the service logs with:

```bash
journalctl -u armareforger-backend.service
```

# Backend Application Setup Guide

## 🚀 Manual Installation Steps

### 1. Terminal Setup
First, open your preferred terminal emulator.

### 2. Navigate to Installation Directory
```bash
cd /opt/ArmAReforgerServerMonitor.Backend
```

### 3. Launch the Application

#### Option A: Self-Contained Executable
If you have a self-contained executable:
```bash
./ArmaReforgerServerMonitor.Backend
```

#### Option B: .NET Framework-Dependent
If you're running a .NET application:
```bash
dotnet ArmaReforgerServerMonitor.Backend.dll
```

### 4. Verify Application Status
> 🔍 The application should now be running and listening on the configured URL  
> (e.g., `http://0.0.0.0:5000`)

### 5. Monitor Logs (Optional)
To continuously monitor application logs:
```bash
tail -f /var/log/your_log_file.log
```
> ⚠️ Replace `/var/log/your_log_file.log` with your actual log file path

---

## ⚡ Important Notes

- You may need `sudo` privileges if:
  - The installation directory requires elevated access
  - The executable needs admin permissions

## 🔧 Troubleshooting

1. Check console output for immediate feedback
2. Monitor log files for detailed runtime information
3. Verify the configured URL is accessible
4. Ensure proper permissions are set

---

*For additional support or configuration options, please refer to the documentation.*

> **Note:** This script installs the .NET 6.0 runtime (if not already installed), verifies log directory permissions, downloads and extracts the backend package from GitHub, sets the proper permissions, and configures a systemd service to run the backend automatically on startup.
