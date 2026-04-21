#!/bin/bash

# Uninstallation script for Asterisk Manager Service

set -e

SERVICE_NAME="asteriskmanager"
INSTALL_DIR="/opt/asteriskmanager"
SYSTEMD_DIR="/etc/systemd/system"

# Check if running as root
if [ "$EUID" -ne 0 ]; then 
    echo "Please run as root (use sudo)"
    exit 1
fi

echo "Uninstalling Asterisk Manager Service..."

# Stop the service if running
if systemctl is-active --quiet "$SERVICE_NAME"; then
    echo "Stopping service..."
    systemctl stop "$SERVICE_NAME"
fi

# Disable the service
if systemctl is-enabled --quiet "$SERVICE_NAME"; then
    echo "Disabling service..."
    systemctl disable "$SERVICE_NAME"
fi

# Remove systemd service file
if [ -f "$SYSTEMD_DIR/$SERVICE_NAME.service" ]; then
    echo "Removing systemd service file..."
    rm "$SYSTEMD_DIR/$SERVICE_NAME.service"
fi

# Reload systemd daemon
echo "Reloading systemd daemon..."
systemctl daemon-reload

# Optionally remove installation directory
read -p "Do you want to remove the installation directory ($INSTALL_DIR)? [y/N] " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo "Removing installation directory..."
    rm -rf "$INSTALL_DIR"
fi

echo "Uninstallation complete!"
