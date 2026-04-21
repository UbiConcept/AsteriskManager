#!/bin/bash

# Installation script for Asterisk Manager Service
# This script sets up the application as a systemd service

set -e

echo "Installing Asterisk Manager Service..."

# Define paths
SERVICE_NAME="asteriskmanager"
INSTALL_DIR="/opt/asteriskmanager"
SERVICE_FILE="$SERVICE_NAME.service"
SYSTEMD_DIR="/etc/systemd/system"

# Check if running as root
if [ "$EUID" -ne 0 ]; then 
    echo "Please run as root (use sudo)"
    exit 1
fi

# Create installation directory if it doesn't exist
if [ ! -d "$INSTALL_DIR" ]; then
    echo "Creating installation directory: $INSTALL_DIR"
    mkdir -p "$INSTALL_DIR"
fi

# Copy application files
echo "Copying application files to $INSTALL_DIR..."
cp -r * "$INSTALL_DIR/"

# Make the executable have execute permissions
chmod +x "$INSTALL_DIR/AsteriskManager"

# Copy systemd service file
echo "Installing systemd service..."
cp "$SERVICE_FILE" "$SYSTEMD_DIR/$SERVICE_FILE"

# Reload systemd daemon
echo "Reloading systemd daemon..."
systemctl daemon-reload

# Enable the service to start on boot
echo "Enabling service to start on boot..."
systemctl enable "$SERVICE_NAME"

# Start the service
echo "Starting service..."
systemctl start "$SERVICE_NAME"

# Show status
echo ""
echo "Installation complete!"
echo ""
echo "Service status:"
systemctl status "$SERVICE_NAME" --no-pager
echo ""
echo "Useful commands:"
echo "  - Check status:   sudo systemctl status $SERVICE_NAME"
echo "  - Stop service:   sudo systemctl stop $SERVICE_NAME"
echo "  - Start service:  sudo systemctl start $SERVICE_NAME"
echo "  - Restart service: sudo systemctl restart $SERVICE_NAME"
echo "  - View logs:      sudo journalctl -u $SERVICE_NAME -f"
echo "  - Disable autostart: sudo systemctl disable $SERVICE_NAME"
