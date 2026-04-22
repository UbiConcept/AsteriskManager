# Create update.zip for testing auto-update functionality
# This script builds the application and creates update.zip

Write-Host "======================================"
Write-Host "Creating update.zip for Auto-Update"
Write-Host "======================================"
Write-Host ""

# Step 1: Build the application
Write-Host "[1/3] Building application..."
dotnet publish -c Release -r linux-arm64 --self-contained -o publish

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    exit 1
}

Write-Host "[2/3] Preparing update package..."

# Create a temporary directory for the update package
$updateDir = "update-package"
if (Test-Path $updateDir) {
    Remove-Item $updateDir -Recurse -Force
}
New-Item -ItemType Directory -Path $updateDir | Out-Null

# Copy essential files to update directory
# Main application files
Copy-Item "publish/AsteriskManager" "$updateDir/" -Force
Copy-Item "publish/AsteriskManager.dll" "$updateDir/" -Force
Copy-Item "publish/AsteriskManager.pdb" "$updateDir/" -Force
Copy-Item "publish/AsteriskManager.deps.json" "$updateDir/" -Force
Copy-Item "publish/AsteriskManager.runtimeconfig.json" "$updateDir/" -Force
Copy-Item "publish/appsettings.json" "$updateDir/" -Force

# Copy all DLL dependencies
Get-ChildItem "publish/*.dll" | Copy-Item -Destination "$updateDir/" -Force

# Copy native libraries
Get-ChildItem "publish/*.so" | Copy-Item -Destination "$updateDir/" -Force

# Copy web content
if (Test-Path "publish/wwwroot") {
    Copy-Item "publish/wwwroot" "$updateDir/" -Recurse -Force
}

# Copy static web assets
if (Test-Path "publish/AsteriskManager.staticwebassets.endpoints.json") {
    Copy-Item "publish/AsteriskManager.staticwebassets.endpoints.json" "$updateDir/" -Force
}

# Copy service files
Copy-Item "asteriskmanager.service" "$updateDir/" -Force
Copy-Item "install.sh" "$updateDir/" -Force
Copy-Item "uninstall.sh" "$updateDir/" -Force

# Step 3: Create ZIP file
Write-Host "[3/3] Creating update.zip..."
$zipFile = "update.zip"
if (Test-Path $zipFile) {
    Remove-Item $zipFile -Force
}

# Use PowerShell's Compress-Archive
Compress-Archive -Path "$updateDir\*" -DestinationPath $zipFile -CompressionLevel Optimal

# Get file size
$fileSize = (Get-Item $zipFile).Length / 1MB
Write-Host ""
Write-Host "✅ Update package created successfully!" -ForegroundColor Green
Write-Host "   File: $zipFile" -ForegroundColor Cyan
Write-Host "   Size: $([math]::Round($fileSize, 2)) MB" -ForegroundColor Cyan
Write-Host ""

# Cleanup
Remove-Item $updateDir -Recurse -Force

Write-Host "To test the update:" -ForegroundColor Yellow
Write-Host "1. Upload update.zip to a web server" -ForegroundColor Yellow
Write-Host "2. Update the UpdateUrl in appsettings.json" -ForegroundColor Yellow
Write-Host "3. Send MQTT UPDATE command or wait for automatic check" -ForegroundColor Yellow
Write-Host ""
Write-Host "MQTT command to trigger update:" -ForegroundColor Yellow
Write-Host "mosquitto_pub -h mqtt.jsmplus.com -p 4546 -u 1C54E63057D0 -P UBIPASS -t 'cmnd/UBI/1C54E63057D0/SIPCMD/UPDATE' -m 'update'" -ForegroundColor Cyan
Write-Host ""
Write-Host "Or copy update.zip to the device for local testing:" -ForegroundColor Yellow
Write-Host "pscp -pw linaro update.zip linaro@192.168.18.39:/tmp/" -ForegroundColor Cyan
