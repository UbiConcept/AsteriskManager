# Asterisk Manager - Installation Guide

This guide explains how to install and configure Asterisk Manager to run automatically on Linux boot.

## Prerequisites

- Linux system with systemd (most modern distributions)
- Root/sudo access
- .NET 10 runtime (included in self-contained build)

## Installation

### Quick Install

1. Extract the application files to a temporary directory
2. Make the installation script executable:
   ```bash
   chmod +x install.sh
   ```
3. Run the installation script with root privileges:
   ```bash
   sudo ./install.sh
   ```

The script will:
- Copy files to `/opt/asteriskmanager`
- Install the systemd service
- Enable auto-start on boot
- Start the service immediately

### Manual Installation

If you prefer to install manually:

1. **Create installation directory:**
   ```bash
   sudo mkdir -p /opt/asteriskmanager
   ```

2. **Copy application files:**
   ```bash
   sudo cp -r * /opt/asteriskmanager/
   ```

3. **Make executable:**
   ```bash
   sudo chmod +x /opt/asteriskmanager/AsteriskManager
   ```

4. **Install systemd service:**
   ```bash
   sudo cp asteriskmanager.service /etc/systemd/system/
   sudo systemctl daemon-reload
   ```

5. **Enable and start service:**
   ```bash
   sudo systemctl enable asteriskmanager
   sudo systemctl start asteriskmanager
   ```

## Service Management

### Check Service Status
```bash
sudo systemctl status asteriskmanager
```

### Start/Stop/Restart Service
```bash
sudo systemctl start asteriskmanager
sudo systemctl stop asteriskmanager
sudo systemctl restart asteriskmanager
```

### View Logs
```bash
# Follow logs in real-time
sudo journalctl -u asteriskmanager -f

# View recent logs
sudo journalctl -u asteriskmanager -n 100

# View logs since boot
sudo journalctl -u asteriskmanager -b
```

### Disable Auto-Start
```bash
sudo systemctl disable asteriskmanager
```

### Re-enable Auto-Start
```bash
sudo systemctl enable asteriskmanager
```

## Uninstallation

To uninstall the service:

```bash
chmod +x uninstall.sh
sudo ./uninstall.sh
```

Or manually:

```bash
sudo systemctl stop asteriskmanager
sudo systemctl disable asteriskmanager
sudo rm /etc/systemd/system/asteriskmanager.service
sudo systemctl daemon-reload
sudo rm -rf /opt/asteriskmanager
```

## Configuration

Configuration files are located in `/opt/asteriskmanager/appsettings.json`.

After modifying configuration, restart the service:
```bash
sudo systemctl restart asteriskmanager
```

## Auto-Update

The application includes auto-update functionality:
- Checks for updates every 60 minutes (configurable)
- Downloads from https://www.ubiconcept.com/update.zip
- Automatically applies updates and restarts
- Can be disabled in appsettings.json

## Troubleshooting

### Service won't start
Check the logs for errors:
```bash
sudo journalctl -u asteriskmanager -n 50
```

### Port already in use
Check if another service is using the same port:
```bash
sudo netstat -tulpn | grep :8080
```

### Permission issues
Ensure the service has proper permissions:
```bash
sudo chown -R root:root /opt/asteriskmanager
sudo chmod +x /opt/asteriskmanager/AsteriskManager
```

## Network Configuration

The application requires:
- Outbound HTTPS access to www.ubiconcept.com (for updates)
- Outbound TCP access to mqtt.jsmplus.com:4546 (for MQTT)
- Inbound HTTP access on port 8080 (or configured port)

## Security Notes

- The service runs as root (required for Asterisk configuration access)
- MQTT credentials are stored in appsettings.json
- Update packages should be served over HTTPS
- Consider firewall rules to restrict access

## Support

For issues or questions, check the application logs first:
```bash
sudo journalctl -u asteriskmanager -f
```
