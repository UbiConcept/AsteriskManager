# Quick deployment script using PuTTY tools (plink/pscp)
# This ensures proper file permissions are set

$DeviceIp = "192.168.18.39"
$Username = "linaro"
$Password = "linaro"

Write-Host "Stopping service..." -ForegroundColor Yellow
plink -batch -pw $Password "$Username@$DeviceIp" "sudo systemctl stop asteriskmanager"

Write-Host "Uploading files..." -ForegroundColor Yellow
pscp -batch -pw $Password publish.tar.gz "$Username@${DeviceIp}:/tmp/"

Write-Host "Extracting and setting permissions..." -ForegroundColor Yellow
plink -batch -pw $Password "$Username@$DeviceIp" @"
sudo tar -xzf /tmp/publish.tar.gz -C /opt/asteriskmanager/ && \
sudo chmod +x /opt/asteriskmanager/AsteriskManager && \
sudo rm /tmp/publish.tar.gz
"@

Write-Host "Starting service..." -ForegroundColor Yellow
plink -batch -pw $Password "$Username@$DeviceIp" "sudo systemctl start asteriskmanager"

Start-Sleep -Seconds 2

Write-Host "`nService Status:" -ForegroundColor Green
plink -batch -pw $Password "$Username@$DeviceIp" "sudo systemctl status asteriskmanager --no-pager"

Write-Host "`nDeployment complete!" -ForegroundColor Green
Write-Host "Web interface: http://${DeviceIp}:8080" -ForegroundColor Cyan
