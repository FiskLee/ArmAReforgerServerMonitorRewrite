#!/bin/bash
# installscript.sh
# This script installs the Arma Reforger Server Monitor backend on Linux.
# It checks prerequisites, prompts for installation and log directories,
# downloads and extracts the release package, and sets up a systemd service.

set -e

# Function: Check for .NET 6.0 Runtime
check_dotnet() {
    if ! command -v dotnet >/dev/null 2>&1; then
        echo ".NET 6.0 Runtime not found. Installing..."
        # Example for Ubuntu; adjust for your distribution.
        wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
        sudo dpkg -i packages-microsoft-prod.deb
        sudo apt-get update
        sudo apt-get install -y dotnet-runtime-6.0
    else
        echo ".NET 6.0 Runtime is installed."
    fi
}

# Prompt user for installation directory (example given)
read -p "Enter installation directory (e.g., /opt/ArmAReforgerServerMonitor.Backend): " INSTALL_DIR
if [ -z "$INSTALL_DIR" ]; then
    echo "Installation directory cannot be empty. Exiting."
    exit 1
fi

# Create installation directory if it doesn't exist
sudo mkdir -p "$INSTALL_DIR"

# Prompt user for master log directory (example given)
read -p "Enter master log directory (e.g., /home/admin/arma-reforger/1874900/AReforgerMaster/logs): " LOG_DIR
if [ -z "$LOG_DIR" ]; then
    echo "Master log directory cannot be empty. Exiting."
    exit 1
fi

# Check if the master log directory exists
if [ ! -d "$LOG_DIR" ]; then
    echo "The master log directory '$LOG_DIR' does not exist. Please create it first and re-run the script."
    exit 1
fi

# Check for prerequisites
check_dotnet

# Define the download URL and temporary file name.
DOWNLOAD_URL="https://github.com/FiskLee/ArmAReforgerServerMonitorRewrite/releases/download/v1.0.0/ArmaReforgerServerMonitor.Backend-linux-x64.zip"
TEMP_ZIP="/tmp/ArmaReforgerServerMonitor.Backend-linux-x64.zip"

echo "Downloading backend package from GitHub..."
wget -O "$TEMP_ZIP" "$DOWNLOAD_URL"

echo "Extracting package to $INSTALL_DIR..."
sudo unzip -o "$TEMP_ZIP" -d "$INSTALL_DIR"

# Remove temporary ZIP file.
rm "$TEMP_ZIP"

# Create a systemd service file.
SERVICE_FILE="/etc/systemd/system/armareforger-backend.service"

echo "Creating systemd service file at $SERVICE_FILE..."
sudo bash -c "cat > $SERVICE_FILE" <<EOF
[Unit]
Description=Arma Reforger Server Monitor Backend Service
After=network.target

[Service]
Type=simple
WorkingDirectory=$INSTALL_DIR
# Set the log directory environment variable (used by the backend)
Environment=LOG_DIR=$LOG_DIR
ExecStart=$INSTALL_DIR/ArmaReforgerServerMonitor.Backend
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF

# Reload systemd to register the new service.
sudo systemctl daemon-reload

# Enable and start the service.
sudo systemctl enable armareforger-backend.service
sudo systemctl start armareforger-backend.service

echo "Installation complete."
echo "Service status can be checked with: sudo systemctl status armareforger-backend.service"
echo "To view logs, run: journalctl -u armareforger-backend.service -f"
