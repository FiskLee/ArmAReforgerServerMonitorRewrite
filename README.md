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
systemctl status armareforgerbackend.service
```

You can also view the service logs with:

```bash
journalctl -u armareforgerbackend.service
```

> **Note:** This script installs the .NET 6.0 runtime (if not already installed), verifies log directory permissions, downloads and extracts the backend package from GitHub, sets the proper permissions, and configures a systemd service to run the backend automatically on startup.
