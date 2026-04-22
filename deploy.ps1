# Deployment script for Asterisk Manager to remote server
# PowerShell version for Windows

param(
    [string]$RemoteHost = "192.168.18.39",
    [string]$RemoteUser = "linaro",
    [string]$InstallDir = "/opt/asteriskmanager"
)

$ErrorActionPreference = "Stop"

Write-Host "======================================"
Write-Host "Asterisk Manager - Remote Deployment"
Write-Host "======================================"
Write-Host "Target: $RemoteUser@$RemoteHost"
Write-Host "Install Directory: $InstallDir"
Write-Host ""

# Check if publish directory exists
if (-not (Test-Path "publish")) {
    Write-Error "Error: Publish directory not found. Please run 'dotnet publish' first."
    exit 1
}

# Copy installation scripts to publish directory
Write-Host "Preparing deployment package..."
Copy-Item "install.sh" "publish/" -Force
Copy-Item "uninstall.sh" "publish/" -Force
Copy-Item "asteriskmanager.service" "publish/" -Force

# Create temporary deployment package
Write-Host "Creating deployment package..."
$TempArchive = "asteriskmanager-deploy.tar.gz"

# Use tar if available (Windows 10+)
if (Get-Command tar -ErrorAction SilentlyContinue) {
    tar -czf $TempArchive -C publish .
} else {
    Write-Error "Error: tar command not found. Please install tar or use WSL."
    exit 1
}

# Copy to remote server
Write-Host "Uploading to $RemoteHost..."
scp $TempArchive "${RemoteUser}@${RemoteHost}:/tmp/"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to upload files. Please ensure SSH is configured."
    Remove-Item $TempArchive -Force
    exit 1
}

# Execute remote installation
Write-Host "Installing on remote server..."
$sshCommands = "set -e && cd /tmp && echo 'Extracting files...' && mkdir -p asteriskmanager-temp && tar -xzf asteriskmanager-deploy.tar.gz -C asteriskmanager-temp && cd asteriskmanager-temp && chmod +x install.sh uninstall.sh && echo 'Running installation script...' && sudo ./install.sh && cd /tmp && rm -rf asteriskmanager-temp asteriskmanager-deploy.tar.gz && echo '' && echo 'Installation complete!' && echo 'Service status:' && systemctl status asteriskmanager --no-pager || true"

ssh "${RemoteUser}@${RemoteHost}" $sshCommands

if ($LASTEXITCODE -ne 0) {
    Write-Error "Installation failed on remote server."
    Remove-Item $TempArchive -Force
    exit 1
}

# Cleanup local temp file
Remove-Item $TempArchive -Force

Write-Host ""
Write-Host "======================================"
Write-Host "Deployment completed successfully!"
Write-Host "======================================"
Write-Host ""
Write-Host "Access the web interface at: http://${RemoteHost}:5000"
Write-Host ""
Write-Host "Useful commands:"
Write-Host "  View logs:    ssh ${RemoteUser}@${RemoteHost} 'journalctl -u asteriskmanager -f'"
Write-Host "  Check status: ssh ${RemoteUser}@${RemoteHost} 'systemctl status asteriskmanager'"
Write-Host "  Restart:      ssh ${RemoteUser}@${RemoteHost} 'systemctl restart asteriskmanager'"
