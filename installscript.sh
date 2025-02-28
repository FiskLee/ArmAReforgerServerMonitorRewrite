#!/bin/bash
# install-backend.sh
# This script installs the Arma Reforger Server Monitor backend on Linux.
# It performs the following steps:
# 1. Checks for prerequisites (like the .NET 6.0 Runtime).
# 2. Prompts the user for an installation directory and a master log directory (with examples).
# 3. Downloads the release package from GitHub.
# 4. Extracts the package into the chosen installation directory.
# 5. Prompts the user whether to create and enable a systemd service.
#    - If yes, it creates a systemd service file, reloads systemd, enables, and starts the service.
#    - If no, it reminds the user how to run the application manually.
#
# Verbose messages are printed for debugging and guidance.

set -euo pipefail

echo "=============================="
echo "Arma Reforger Server Monitor Backend Installer"
echo "=============================="

# Function to check for .NET 6.0 Runtime
check_dotnet() {
    echo "[DEBUG] Checking for .NET 6.0 Runtime..."
    if ! command -v dotnet >/dev/null 2>&1; then
        echo "[INFO] .NET 6.0 Runtime not found. Installing..."
        # Example for Ubuntu. Adjust for your distribution.
        wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb
        sudo dpkg -i /tmp/packages-microsoft-prod.deb
        sudo apt-get update
        sudo apt-get install -y dotnet-runtime-6.0
    else
        echo "[INFO] .NET 6.0 Runtime is already installed."
    fi
}

# Prompt for installation directory with an example.
read -rp "Enter installation directory (e.g., /opt/ArmAReforgerServerMonitor.Backend): " INSTALL_DIR
if [ -z "$INSTALL_DIR" ]; then
    echo "[ERROR] Installation directory cannot be empty. Exiting."
    exit 1
fi
echo "[DEBUG] Installation directory set to: $INSTALL_DIR"
sudo mkdir -p "$INSTALL_DIR"

# Prompt for master log directory with an example.
read -rp "Enter master log directory (e.g., /home/admin/arma-reforger/1874900/AReforgerMaster/logs): " LOG_DIR
if [ -z "$LOG_DIR" ]; then
    echo "[ERROR] Master log directory cannot be empty. Exiting."
    exit 1
fi
echo "[DEBUG] Master log directory set to: $LOG_DIR"
if [ ! -d "$LOG_DIR" ]; then
    echo "[ERROR] The master log directory '$LOG_DIR' does not exist. Please create it and re-run the script."
    exit 1
fi

# Check prerequisites.
check_dotnet

# Define download URL and temporary file.
DOWNLOAD_URL="https://github.com/FiskLee/ArmAReforgerServerMonitorRewrite/releases/download/v1.0.0/ArmaReforgerServerMonitor.Backend-linux-x64.zip"
TEMP_ZIP="/tmp/ArmaReforgerServerMonitor.Backend-linux-x64.zip"

echo "[INFO] Downloading backend package from GitHub..."
wget -O "$TEMP_ZIP" "$DOWNLOAD_URL" --verbose

echo "[INFO] Extracting package to $INSTALL_DIR..."
sudo unzip -o "$TEMP_ZIP" -d "$INSTALL_DIR" | tee /tmp/extract.log

echo "[DEBUG] Removing temporary ZIP file..."
rm "$TEMP_ZIP"

# Ask the user if they want to create and enable a systemd service.
echo ""
read -rp "Do you want to create and enable a systemd service? (y/n): " CREATE_SERVICE
if [[ "$CREATE_SERVICE" =~ ^[Yy]$ ]]; then
    # Create systemd service file.
    SERVICE_FILE="/etc/systemd/system/armareforger-backend.service"
    echo "[INFO] Creating systemd service file at $SERVICE_FILE..."
    sudo bash -c "cat > $SERVICE_FILE" <<EOF
[Unit]
Description=Arma Reforger Server Monitor Backend Service
After=network.target

[Service]
Type=simple
WorkingDirectory=$INSTALL_DIR
Environment=LOG_DIR=$LOG_DIR
ExecStart=$INSTALL_DIR/ArmaReforgerServerMonitor.Backend
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF

    echo "[DEBUG] Reloading systemd daemon..."
    sudo systemctl daemon-reload

    echo "[INFO] Enabling and starting the service..."
    sudo systemctl enable armareforger-backend.service
    sudo systemctl start armareforger-backend.service

    echo "=============================="
    echo "Installation complete."
    echo "Service status: sudo systemctl status armareforger-backend.service"
    echo "To view logs: journalctl -u armareforger-backend.service -f"
    echo "=============================="
else
    echo "[INFO] Skipping systemd service creation. You can run the application manually:"
    echo "  $INSTALL_DIR/ArmaReforgerServerMonitor.Backend"
    echo "=============================="
fi
