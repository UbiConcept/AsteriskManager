# Automated update with embedded credentials
$host = "192.168.18.39"
$user = "linaro"
$pass = "linaro"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Asterisk Manager - Automated Update" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Target: $user@$host" -ForegroundColor Yellow
Write-Host ""

# Check if plink/pscp are available (from PuTTY)
$usePutty = $false
if (Get-Command plink -ErrorAction SilentlyContinue) {
    $usePutty = $true
    Write-Host "Using PuTTY tools..." -ForegroundColor Green
}

if ($usePutty) {
    # Using PuTTY which supports password parameter
    Write-Host "[1/3] Uploading update package..." -ForegroundColor Yellow
    & pscp -pw $pass asteriskmanager-update.tar.gz "${user}@${host}:/tmp/"
    
    Write-Host "[2/3] Extracting and stopping old service..." -ForegroundColor Yellow
    $cmd1 = "sudo systemctl stop asteriskmanager; sudo pkill -9 AsteriskManager; sleep 2; cd /tmp && mkdir -p asteriskmanager-temp && tar -xzf asteriskmanager-update.tar.gz -C asteriskmanager-temp"
    & plink -batch -pw $pass "${user}@${host}" $cmd1
    
    Write-Host "[3/3] Installing update and starting service..." -ForegroundColor Yellow
    $cmd2 = "cd /tmp/asteriskmanager-temp && sudo rm -rf /opt/asteriskmanager/* && sudo cp -r * /opt/asteriskmanager/ && sudo chmod +x /opt/asteriskmanager/AsteriskManager && sudo systemctl daemon-reload && sudo systemctl start asteriskmanager && sleep 3 && sudo systemctl status asteriskmanager --no-pager"
    & plink -batch -pw $pass "${user}@${host}" $cmd2
    
} else {
    # Fallback: Create expect-like script for OpenSSH
    Write-Host "PuTTY not found. Using alternative method..." -ForegroundColor Yellow
    
    # Create a temporary script that handles the password
    $tempScript = @"
#!/usr/bin/expect -f
set timeout 30
set password "$pass"

# Upload file
spawn scp asteriskmanager-update.tar.gz ${user}@${host}:/tmp/
expect {
    "password:" { send "`$password\r" }
    "Password:" { send "`$password\r" }
}
expect eof

# Run update commands
spawn ssh ${user}@${host} "sudo systemctl stop asteriskmanager; sudo pkill -9 AsteriskManager; sleep 2; cd /tmp && mkdir -p asteriskmanager-temp && tar -xzf asteriskmanager-update.tar.gz -C asteriskmanager-temp && cd asteriskmanager-temp && sudo rm -rf /opt/asteriskmanager/* && sudo cp -r * /opt/asteriskmanager/ && sudo chmod +x /opt/asteriskmanager/AsteriskManager && sudo systemctl daemon-reload && sudo systemctl start asteriskmanager && sleep 3 && sudo systemctl status asteriskmanager --no-pager"
expect {
    "password:" { send "`$password\r" }
    "Password:" { send "`$password\r" }
}
expect eof
"@

    Write-Host ""
    Write-Host "ERROR: This method requires either:" -ForegroundColor Red
    Write-Host "  1. Install PuTTY (includes pscp/plink): https://www.putty.org/" -ForegroundColor Yellow
    Write-Host "  2. Or manually run the commands below:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "MANUAL STEPS (password is: linaro)" -ForegroundColor Cyan
    Write-Host "================================" -ForegroundColor Cyan
    Write-Host "scp asteriskmanager-update.tar.gz linaro@192.168.18.39:/tmp/"
    Write-Host 'ssh linaro@192.168.18.39 "sudo systemctl stop asteriskmanager && sudo pkill -9 AsteriskManager && cd /tmp && mkdir -p asteriskmanager-temp && tar -xzf asteriskmanager-update.tar.gz -C asteriskmanager-temp && cd asteriskmanager-temp && sudo rm -rf /opt/asteriskmanager/* && sudo cp -r * /opt/asteriskmanager/ && sudo chmod +x /opt/asteriskmanager/AsteriskManager && sudo systemctl start asteriskmanager"'
    Write-Host ""
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Update Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "Web Interface: http://$host:5000" -ForegroundColor Cyan
