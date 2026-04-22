#!/bin/bash

# Deployment script for Asterisk Manager to remote server

set -e

# Configuration
REMOTE_HOST="192.168.18.39"
REMOTE_USER="root"
INSTALL_DIR="/opt/asteriskmanager"
LOCAL_PUBLISH_DIR="publish"

echo "======================================"
echo "Asterisk Manager - Remote Deployment"
echo "======================================"
echo "Target: $REMOTE_USER@$REMOTE_HOST"
echo "Install Directory: $INSTALL_DIR"
echo ""

# Check if publish directory exists
if [ ! -d "$LOCAL_PUBLISH_DIR" ]; then
    echo "Error: Publish directory not found. Please run 'dotnet publish' first."
    exit 1
fi

# Copy installation scripts to publish directory
echo "Preparing deployment package..."
cp install.sh "$LOCAL_PUBLISH_DIR/"
cp uninstall.sh "$LOCAL_PUBLISH_DIR/"
cp asteriskmanager.service "$LOCAL_PUBLISH_DIR/"

# Create temporary deployment package
echo "Creating deployment package..."
TEMP_ARCHIVE="asteriskmanager-deploy.tar.gz"
tar -czf "$TEMP_ARCHIVE" -C "$LOCAL_PUBLISH_DIR" .

# Copy to remote server
echo "Uploading to $REMOTE_HOST..."
scp "$TEMP_ARCHIVE" "$REMOTE_USER@$REMOTE_HOST:/tmp/"

# Execute remote installation
echo "Installing on remote server..."
ssh "$REMOTE_USER@$REMOTE_HOST" << 'ENDSSH'
    set -e
    cd /tmp
    
    # Extract files
    echo "Extracting files..."
    mkdir -p asteriskmanager-temp
    tar -xzf asteriskmanager-deploy.tar.gz -C asteriskmanager-temp
    cd asteriskmanager-temp
    
    # Make scripts executable
    chmod +x install.sh uninstall.sh
    
    # Run installation
    echo "Running installation script..."
    ./install.sh
    
    # Cleanup
    cd /tmp
    rm -rf asteriskmanager-temp asteriskmanager-deploy.tar.gz
    
    echo ""
    echo "Installation complete!"
    echo "Service status:"
    systemctl status asteriskmanager --no-pager || true
ENDSSH

# Cleanup local temp file
rm "$TEMP_ARCHIVE"

echo ""
echo "======================================"
echo "Deployment completed successfully!"
echo "======================================"
echo ""
echo "Access the web interface at: http://$REMOTE_HOST:5000"
echo ""
echo "Useful commands:"
echo "  View logs:    ssh $REMOTE_USER@$REMOTE_HOST 'journalctl -u asteriskmanager -f'"
echo "  Check status: ssh $REMOTE_USER@$REMOTE_HOST 'systemctl status asteriskmanager'"
echo "  Restart:      ssh $REMOTE_USER@$REMOTE_HOST 'systemctl restart asteriskmanager'"
