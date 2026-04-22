# Test Auto-Update Functionality
# This script simulates the update process locally on the device

param(
    [string]$RemoteHost = "192.168.18.39",
    [string]$RemoteUser = "linaro",
    [string]$RemotePassword = "linaro"
)

$ErrorActionPreference = "Stop"

Write-Host "======================================"
Write-Host "Testing Auto-Update Functionality"
Write-Host "======================================"
Write-Host "Target: $RemoteUser@$RemoteHost"
Write-Host ""

# Check if update.zip exists
if (-not (Test-Path "update.zip")) {
    Write-Error "update.zip not found! Run create-update-package.ps1 first."
    exit 1
}

$fileSize = (Get-Item "update.zip").Length / 1MB
Write-Host "[1/5] Uploading update.zip ($([math]::Round($fileSize, 2)) MB)..."
pscp -pw $RemotePassword update.zip "${RemoteUser}@${RemoteHost}:/tmp/update.zip"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Upload failed!"
    exit 1
}

Write-Host "[2/5] Setting up local web server on device..."
$setupScript = @'
#!/bin/bash
set -e

# Kill any existing Python web servers
sudo pkill -f "python.*SimpleHTTPServer" 2>/dev/null || true
sudo pkill -f "python.*http.server" 2>/dev/null || true

# Start a simple HTTP server on port 8080
cd /tmp
nohup python3 -m http.server 8080 > /tmp/http-server.log 2>&1 &

sleep 2
echo "Local web server started on port 8080"
'@

plink -ssh -pw $RemotePassword "${RemoteUser}@${RemoteHost}" $setupScript

Write-Host "[3/5] Updating appsettings.json to use local update server..."
$updateConfig = @'
#!/bin/bash
set -e

# Backup current appsettings.json
sudo cp /opt/asteriskmanager/appsettings.json /opt/asteriskmanager/appsettings.json.backup

# Update the UpdateUrl to point to localhost
sudo sed -i 's|"UpdateUrl": ".*"|"UpdateUrl": "http://127.0.0.1:8080"|g' /opt/asteriskmanager/appsettings.json

echo "Configuration updated to use local update server"
cat /opt/asteriskmanager/appsettings.json | grep UpdateUrl
'@

plink -ssh -pw $RemotePassword "${RemoteUser}@${RemoteHost}" $updateConfig

Write-Host "[4/5] Restarting service to apply configuration..."
plink -ssh -pw $RemotePassword "${RemoteUser}@${RemoteHost}" "sudo systemctl restart asteriskmanager && sleep 3"

Write-Host "[5/5] Triggering update via MQTT..."
Write-Host ""
Write-Host "Publishing UPDATE command to MQTT..." -ForegroundColor Yellow
Write-Host "Topic: cmnd/UBI/1C54E63057D0/SIPCMD/UPDATE" -ForegroundColor Cyan
Write-Host ""

# Try to publish MQTT command (requires mosquitto_pub)
try {
    & mosquitto_pub -h mqtt.jsmplus.com -p 4546 -u 1C54E63057D0 -P UBIPASS -t "cmnd/UBI/1C54E63057D0/SIPCMD/UPDATE" -m "update"
    Write-Host "✅ MQTT command sent successfully!" -ForegroundColor Green
} catch {
    Write-Warning "mosquitto_pub not found. You can manually trigger the update:"
    Write-Host "mosquitto_pub -h mqtt.jsmplus.com -p 4546 -u 1C54E63057D0 -P UBIPASS -t 'cmnd/UBI/1C54E63057D0/SIPCMD/UPDATE' -m 'update'" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Monitoring logs for update process..." -ForegroundColor Yellow
Write-Host "Press Ctrl+C to stop monitoring" -ForegroundColor Yellow
Write-Host ""

Start-Sleep -Seconds 3

# Monitor logs
plink -ssh -pw $RemotePassword "${RemoteUser}@${RemoteHost}" "sudo journalctl -u asteriskmanager -f"
