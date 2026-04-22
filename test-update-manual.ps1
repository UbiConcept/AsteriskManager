# Manual Update Test - Simple approach
# This uploads update.zip and provides instructions

param(
    [string]$RemoteHost = "192.168.18.39",
    [string]$RemoteUser = "linaro",
    [string]$RemotePassword = "linaro"
)

Write-Host "======================================"
Write-Host "Manual Update Test Setup"
Write-Host "======================================"
Write-Host ""

if (-not (Test-Path "update.zip")) {
    Write-Error "update.zip not found! Run create-update-package.ps1 first."
    exit 1
}

Write-Host "[1/3] Uploading update.zip to device..."
pscp -pw $RemotePassword update.zip "${RemoteUser}@${RemoteHost}:/tmp/update.zip"

Write-Host "[2/3] Creating test directory and extracting..."
$testScript = @'
#!/bin/bash
set -e

# Create test directory
mkdir -p /tmp/update-test
cd /tmp/update-test

# Extract update.zip
unzip -o /tmp/update.zip

echo ""
echo "✅ Update package extracted to /tmp/update-test"
echo ""
echo "Contents:"
ls -lh | head -20

echo ""
echo "Total files:"
ls -1 | wc -l
'@

plink -ssh -pw $RemotePassword "${RemoteUser}@${RemoteHost}" $testScript

Write-Host ""
Write-Host "[3/3] Update package ready for testing!" -ForegroundColor Green
Write-Host ""
Write-Host "=" * 60 -ForegroundColor Yellow
Write-Host "TESTING OPTIONS:" -ForegroundColor Yellow
Write-Host "=" * 60 -ForegroundColor Yellow
Write-Host ""

Write-Host "Option 1: Test via MQTT (Recommended)" -ForegroundColor Cyan
Write-Host "-" * 60
Write-Host "1. Ensure update.zip is accessible via HTTP"
Write-Host "   You can use: python3 -m http.server 8080"
Write-Host ""
Write-Host "2. Update appsettings.json UpdateUrl"
Write-Host ""
Write-Host "3. Send MQTT command:"
Write-Host "   mosquitto_pub -h mqtt.jsmplus.com -p 4546 \" -ForegroundColor Green
Write-Host "     -u 1C54E63057D0 -P UBIPASS \" -ForegroundColor Green
Write-Host "     -t 'cmnd/UBI/1C54E63057D0/SIPCMD/UPDATE' \" -ForegroundColor Green
Write-Host "     -m 'update'" -ForegroundColor Green
Write-Host ""

Write-Host "Option 2: Manual Update Simulation" -ForegroundColor Cyan
Write-Host "-" * 60
Write-Host "SSH into device and run:"
Write-Host "  ssh linaro@$RemoteHost" -ForegroundColor Green
Write-Host ""
Write-Host "Then execute:"
Write-Host "  cd /tmp/update-test" -ForegroundColor Green
Write-Host "  sudo systemctl stop asteriskmanager" -ForegroundColor Green
Write-Host "  sudo cp -r * /opt/asteriskmanager/" -ForegroundColor Green
Write-Host "  sudo chmod +x /opt/asteriskmanager/AsteriskManager" -ForegroundColor Green
Write-Host "  sudo systemctl start asteriskmanager" -ForegroundColor Green
Write-Host "  sudo systemctl status asteriskmanager" -ForegroundColor Green
Write-Host ""

Write-Host "Option 3: Monitor Auto-Update Service" -ForegroundColor Cyan
Write-Host "-" * 60
Write-Host "View logs to see update process:"
Write-Host "  plink -ssh -pw $RemotePassword ${RemoteUser}@${RemoteHost} 'sudo journalctl -u asteriskmanager -f'" -ForegroundColor Green
Write-Host ""

Write-Host "=" * 60 -ForegroundColor Yellow
Write-Host ""
