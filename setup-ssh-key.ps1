# Script to set up SSH key authentication
# Run this to avoid password prompts in the future

Write-Host "Setting up SSH key authentication..."
Write-Host ""

$sshDir = "$env:USERPROFILE\.ssh"
$keyFile = "$sshDir\id_ed25519"

# Check if SSH directory exists
if (-not (Test-Path $sshDir)) {
    New-Item -ItemType Directory -Path $sshDir | Out-Null
}

# Check if key already exists
if (Test-Path $keyFile) {
    Write-Host "SSH key already exists at $keyFile"
    $response = Read-Host "Do you want to use the existing key? (y/n)"
    if ($response -ne 'y') {
        exit 0
    }
} else {
    Write-Host "Generating new SSH key..."
    ssh-keygen -t ed25519 -f $keyFile -N '""'
}

# Copy public key to server
Write-Host ""
Write-Host "Copying public key to server..."
Write-Host "You will be prompted for the server password one last time."
Write-Host ""

$remoteHost = Read-Host "Enter remote host (default: 192.168.18.39)"
if ([string]::IsNullOrWhiteSpace($remoteHost)) {
    $remoteHost = "192.168.18.39"
}

$remoteUser = Read-Host "Enter remote user (default: root)"
if ([string]::IsNullOrWhiteSpace($remoteUser)) {
    $remoteUser = "root"
}

$pubKey = Get-Content "$keyFile.pub"
$sshCommand = "mkdir -p ~/.ssh && echo '$pubKey' >> ~/.ssh/authorized_keys && chmod 700 ~/.ssh && chmod 600 ~/.ssh/authorized_keys"

ssh "$remoteUser@$remoteHost" $sshCommand

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✓ SSH key authentication set up successfully!"
    Write-Host "You can now connect without a password."
    Write-Host ""
    Write-Host "Try running: .\deploy.ps1"
} else {
    Write-Host ""
    Write-Host "✗ Failed to set up SSH key authentication."
    Write-Host "Please check your credentials and try again."
}
