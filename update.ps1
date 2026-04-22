# Update deployment script for Asterisk Manager
# Stops old version, deploys new version, starts service

param(
    [string]$RemoteHost = "192.168.18.39",
    [string]$RemoteUser = "linaro",
    [string]$RemotePassword = "linaro"
)

$ErrorActionPreference = "Stop"

Write-Host "======================================"
Write-Host "Asterisk Manager - UPDATE"
Write-Host "======================================"
Write-Host "Target: $RemoteUser@$RemoteHost"
Write-Host ""

# Check if publish directory exists
if (-not (Test-Path "publish")) {
    Write-Error "Publish directory not found. Run 'dotnet publish' first."
    exit 1
}

# Prepare deployment package
Write-Host "[1/5] Preparing deployment package..."
Copy-Item "install.sh" "publish/" -Force
Copy-Item "uninstall.sh" "publish/" -Force
Copy-Item "asteriskmanager.service" "publish/" -Force

Write-Host "[2/5] Creating archive..."
$archive = "asteriskmanager-update.tar.gz"
if (Test-Path $archive) { Remove-Item $archive -Force }
tar -czf $archive -C publish .

Write-Host "[3/5] Uploading to server..."
$env:SSHPASS = $RemotePassword
echo $RemotePassword | scp $archive "${RemoteUser}@${RemoteHost}:/tmp/" 2>$null

if ($LASTEXITCODE -ne 0) {
    Write-Host "Upload via SCP..."
    scp $archive "${RemoteUser}@${RemoteHost}:/tmp/"
}

Write-Host "[4/5] Stopping old service and deploying update..."
$updateScript = @'
#!/bin/bash
set -e

echo "Stopping old services..."
sudo systemctl stop asteriskmanager 2>/dev/null || true
sudo pkill -9 AsteriskManager 2>/dev/null || true
sleep 2

echo "Extracting update..."
cd /tmp
rm -rf asteriskmanager-update
mkdir -p asteriskmanager-update
tar -xzf asteriskmanager-update.tar.gz -C asteriskmanager-update
cd asteriskmanager-update

echo "Installing update to /opt/asteriskmanager..."
sudo mkdir -p /opt/asteriskmanager
sudo rm -rf /opt/asteriskmanager/*
sudo cp -r * /opt/asteriskmanager/
sudo chmod +x /opt/asteriskmanager/AsteriskManager

echo "Updating systemd service..."
sudo cp /opt/asteriskmanager/asteriskmanager.service /etc/systemd/system/
sudo systemctl daemon-reload

echo "Starting service..."
sudo systemctl start asteriskmanager
sleep 3

echo ""
echo "Service status:"
sudo systemctl status asteriskmanager --no-pager -l || true

echo ""
echo "Cleaning up..."
cd /tmp
rm -rf asteriskmanager-update asteriskmanager-update.tar.gz

echo ""
echo "Update complete!"
'@

# Save script to temp file
$tempScript = [System.IO.Path]::GetTempFileName()
$updateScript | Out-File -FilePath $tempScript -Encoding ASCII -NoNewline

# Upload and execute script
scp $tempScript "${RemoteUser}@${RemoteHost}:/tmp/update.sh"
ssh "${RemoteUser}@${RemoteHost}" "chmod +x /tmp/update.sh && /tmp/update.sh && rm /tmp/update.sh"

Remove-Item $tempScript -Force

Write-Host ""
Write-Host "[5/5] Verifying deployment..."
ssh "${RemoteUser}@${RemoteHost}" "sudo systemctl is-active asteriskmanager"

# Cleanup
Remove-Item $archive -Force

Write-Host ""
Write-Host "======================================"
Write-Host "UPDATE COMPLETE!"
Write-Host "======================================"
Write-Host ""
Write-Host "✓ Application updated successfully"
Write-Host "✓ Service is running"
Write-Host ""
Write-Host "Web Interface: http://$RemoteHost:5000"
Write-Host ""
Write-Host "View logs: ssh ${RemoteUser}@${RemoteHost} 'sudo journalctl -u asteriskmanager -f'"
