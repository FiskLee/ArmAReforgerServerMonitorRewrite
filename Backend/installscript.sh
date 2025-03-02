#!/bin/bash
# install-backend.sh
# This script installs the required prerequisites, downloads the ArmaReforgerServerMonitor.Backend package,
# installs it to a specified directory, and sets up a systemd service.

set -e

### Variables ###
PACKAGE_URL="https://github.com/FiskLee/ArmAReforgerServerMonitorRewrite/releases/download/v1.0.0/ArmaReforgerServerMonitor.Backend-linux-x64.zip"
PACKAGE_NAME="ArmaReforgerServerMonitor.Backend-linux-x64.zip"
INSTALL_DIR="/opt/ArmaReforgerServerMonitor.Backend"
SERVICE_FILE="/etc/systemd/system/armareforgerbackend.service"
BINARY_NAME="ArmaReforgerServerMonitor.Backend"  # Adjust if the binary name is different
LOG_DIR="/var/log/armareforger"  # Adjust as needed for your log directories

### Functions ###

# Function to check and install .NET 6.0 runtime (for Debian/Ubuntu-based systems)
install_dotnet_runtime() {
    echo "Checking for .NET 6.0 runtime..."
    if ! command -v dotnet &>/dev/null; then
        echo ".NET 6.0 is not installed. Installing..."
        wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
        sudo dpkg -i packages-microsoft-prod.deb
        sudo apt-get update
        sudo apt-get install -y apt-transport-https
        sudo apt-get update
        sudo apt-get install -y dotnet-runtime-6.0
    else
        # Check version of dotnet
        DOTNET_VERSION=$(dotnet --list-runtimes | grep "Microsoft.NETCore.App" | head -n1 | awk '{print $2}')
        if [[ "$DOTNET_VERSION" < "6.0.0" ]]; then
            echo ".NET runtime version is less than 6.0. Installing .NET 6.0..."
            sudo apt-get install -y dotnet-runtime-6.0
        else
            echo ".NET 6.0 runtime is installed."
        fi
    fi
}

# Function to verify that the current user has read access to the log directory
verify_log_permissions() {
    echo "Verifying read access to log directory: $LOG_DIR"
    if [ ! -d "$LOG_DIR" ]; then
        echo "Log directory $LOG_DIR does not exist. Please ensure the directory exists and is accessible."
        exit 1
    fi

    if [ ! -r "$LOG_DIR" ]; then
        echo "Insufficient permissions to read $LOG_DIR. Please adjust the permissions."
        exit 1
    fi
}

### Main Script ###

# Ensure the script is run as root
if [ "$EUID" -ne 0 ]; then
    echo "Please run as root (e.g., using sudo)."
    exit 1
fi

echo "Installing prerequisites..."
install_dotnet_runtime
verify_log_permissions

echo "Downloading package from GitHub..."
wget -O "$PACKAGE_NAME" "$PACKAGE_URL"

echo "Creating installation directory at $INSTALL_DIR..."
mkdir -p "$INSTALL_DIR"

echo "Extracting package..."
unzip -o "$PACKAGE_NAME" -d "$INSTALL_DIR"

echo "Setting executable permissions for $BINARY_NAME..."
chmod +x "$INSTALL_DIR/$BINARY_NAME"

echo "Creating systemd service file at $SERVICE_FILE..."
cat > "$SERVICE_FILE" <<EOL
[Unit]
Description=Arma Reforger Server Monitor Backend
After=network.target

[Service]
Type=simple
WorkingDirectory=$INSTALL_DIR
ExecStart=$INSTALL_DIR/$BINARY_NAME
Restart=on-failure
User=root

[Install]
WantedBy=multi-user.target
EOL

echo "Reloading systemd daemon..."
systemctl daemon-reload

echo "Enabling and starting service..."
systemctl enable armareforgerbackend.service
systemctl start armareforgerbackend.service

echo "Installation complete."
echo "You can check the service status with: systemctl status armareforgerbackend.service"
