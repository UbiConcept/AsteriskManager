# Complete deployment script for Asterisk Manager
# Handles existing installations and port conflicts

param(
    [string]$RemoteHost = "192.168.18.39",
    [string]$RemoteUser = "linaro",
    [string]$RemotePassword = "linaro"
)

$ErrorActionPreference = "Stop"

Write-Host "======================================"
Write-Host "Asterisk Manager - Deployment"
Write-Host "======================================"
Write-Host "Target: $RemoteUser@$RemoteHost"
Write-Host ""

# Prepare deployment package
Write-Host "Preparing deployment package..."
Copy-Item "install.sh" "publish/" -Force
Copy-Item "uninstall.sh" "publish/" -Force
Copy-Item "asteriskmanager.service" "publish/" -Force

Write-Host "Creating archive..."
$archive = "asteriskmanager-deploy.tar.gz"
tar -czf $archive -C publish .

# Upload to server
Write-Host "Uploading to server..."
echo $RemotePassword | scp $archive "${RemoteUser}@${RemoteHost}:/tmp/"

# Deploy and install
Write-Host "Installing on remote server..."
$deployScript = @'
cd /tmp
rm -rf asteriskmanager-temp
mkdir -p asteriskmanager-temp
tar -xzf asteriskmanager-deploy.tar.gz -C asteriskmanager-temp
cd asteriskmanager-temp

# Stop any existing service
echo "Stopping existing services..."
sudo systemctl stop asteriskmanager 2>/dev/null || true
sudo pkill -9 AsteriskManager 2>/dev/null || true
sleep 2

# Install
echo "Installing to /opt/asteriskmanager..."
sudo mkdir -p /opt/asteriskmanager
sudo cp -r * /opt/asteriskmanager/
sudo chmod +x /opt/asteriskmanager/AsteriskManager

# Install systemd service
echo "Installing systemd service..."
sudo cp /opt/asteriskmanager/asteriskmanager.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable asteriskmanager

# Start service
echo "Starting service..."
sudo systemctl start asteriskmanager
sleep 3

# Show status
echo ""
echo "Installation complete!"
echo "Service status:"
sudo systemctl status asteriskmanager --no-pager -l

# Cleanup
cd /tmp
rm -rf asteriskmanager-temp asteriskmanager-deploy.tar.gz
'@

echo $RemotePassword | ssh "${RemoteUser}@${RemoteHost}" $deployScript

# Cleanup
Remove-Item $archive -Force

Write-Host ""
Write-Host "======================================"
Write-Host "Deployment Complete!"
Write-Host "======================================"
Write-Host ""
Write-Host "Web Interface: http://$RemoteHost:5000"
Write-Host ""
Write-Host "Useful commands:"
Write-Host "  ssh ${RemoteUser}@${RemoteHost} 'sudo journalctl -u asteriskmanager -f'"
Write-Host "  ssh ${RemoteUser}@${RemoteHost} 'sudo systemctl status asteriskmanager'"
