# Asterisk Manager

A Blazor Server application for managing Asterisk PBX configurations with MQTT integration and auto-update capabilities.

## Version

**Current Version:** 1.0.17

## Features

- 🌐 **Blazor Server Web Interface** - Modern, responsive web UI for Asterisk management
- 📞 **PJSIP Extension Management** - Create, edit, and manage SIP extensions
- 📝 **Configuration Editor** - Direct editing of Asterisk configuration files
- 📊 **Log Viewer** - Real-time viewing of Asterisk logs and live system logs
- 🔧 **Service Management** - Restart Asterisk service from the web interface
- 📡 **MQTT Integration** - Remote command and control via MQTT
  - Subscribes to: `cmnd/UBI/{MacAddress}/SIPCMD/#`
  - Heartbeat: `tele/UBI/{MacAddress}/HEARTBEAT` (every 60 seconds with version info)
  - **Remote Updates**: Trigger software updates via MQTT command (see [MQTT-UPDATE.md](MQTT-UPDATE.md))
  - **Remote Extension Management**: Add/modify extensions via MQTT (see [MQTT-EXTENSION.md](MQTT-EXTENSION.md))
- 🔄 **Auto-Update** - Automatic updates from ubiconcept.com
- 📶 **WiFi Management** - Configure wireless network settings
- 🚀 **Systemd Integration** - Auto-start on Linux boot
- 🔐 **Secure** - MAC address-based authentication for MQTT

## Requirements

- Linux system with systemd (Raspberry Pi, Ubuntu, Debian, etc.)
- .NET 10 runtime (included in self-contained build)
- Network access to MQTT broker (mqtt.jsmplus.com:4546)
- Root access for Asterisk configuration management

## Installation

See [INSTALL.md](INSTALL.md) for detailed installation instructions.

### Quick Start

```bash
# Make the installation script executable
chmod +x install.sh

# Run the installer with sudo
sudo ./install.sh
```

The application will be installed to `/opt/asteriskmanager` and configured to start automatically on boot.

## Configuration

Configuration is managed through `appsettings.json`:

```json
{
  "Asterisk": {
    "ExtensionsPath": "/etc/asterisk/extensions.conf",
    "PjsipPath": "/etc/asterisk/pjsip.conf",
    "LogPath": "/var/log/asterisk/full",
    "MessageLogPath": "/var/log/asterisk/messages.log"
  },
  "Mqtt": {
    "Server": "mqtt.jsmplus.com",
    "Port": 4546,
    "Password": "UBIPASS"
  },
  "AutoUpdate": {
    "UpdateUrl": "https://www.ubiconcept.com",
    "Enabled": true,
    "CheckIntervalMinutes": 60
  }
}
```

## Usage

### Web Interface

Access the web interface at `http://<device-ip>:8080`

### Service Management

```bash
# Check status
sudo systemctl status asteriskmanager

# Start/Stop/Restart
sudo systemctl start asteriskmanager
sudo systemctl stop asteriskmanager
sudo systemctl restart asteriskmanager

# View logs
sudo journalctl -u asteriskmanager -f
```

## MQTT Topics

### Subscribed Topics
- `cmnd/UBI/{MacAddress}/SIPCMD/#` - Receives SIP commands

### Published Topics
- `tele/UBI/{MacAddress}/HEARTBEAT` - Heartbeat with UTC timestamp (every 60 seconds)

### Remote Update Trigger
You can trigger a software update remotely via MQTT. See [MQTT-UPDATE.md](MQTT-UPDATE.md) for details.

```bash
mosquitto_pub -h mqtt.jsmplus.com -p 4546 -u {MacAddress} -P UBIPASS \
  -t "cmnd/UBI/{MacAddress}/SIPCMD/UPDATE" -m "update"
```

## Auto-Update

The application automatically checks for updates every hour. Update packages should be:
- Hosted at: `https://www.ubiconcept.com/update.zip`
- Format: ZIP archive containing updated application files
- The service will automatically download, extract, and restart

## Development

Built with:
- .NET 10
- Blazor Server
- MQTTnet 5.1.0
- SignalR

## Project Structure

```
AsteriskManager/
├── Components/          # Blazor components and pages
│   ├── Layout/         # Layout components
│   └── Pages/          # Application pages
├── Services/           # Background services and business logic
├── wwwroot/            # Static assets
├── appsettings.json    # Configuration
└── Program.cs          # Application entry point
```

## License

Copyright © 2024. All rights reserved.

## Support

For installation help, see [INSTALL.md](INSTALL.md).

For issues:
1. Check application logs: `sudo journalctl -u asteriskmanager -f`
2. Verify network connectivity to MQTT broker
3. Ensure proper permissions on Asterisk configuration files

### Common Issues

#### Service fails to start with "Permission denied"

If the service fails to start with:
```
Failed at step EXEC spawning /opt/asteriskmanager/AsteriskManager: Permission denied
```

This means the executable flag is missing. Fix it with:
```bash
sudo chmod +x /opt/asteriskmanager/AsteriskManager
sudo systemctl restart asteriskmanager
```

**Note**: This can happen when deploying manually with tar from Windows, as tar doesn't preserve Unix execute permissions. The auto-update mechanism handles this automatically.

#### Using quick-deploy.ps1

For manual deployments from Windows, use the included `quick-deploy.ps1` script which automatically sets correct permissions:
```powershell
.\quick-deploy.ps1
```

This script stops the service, uploads files, extracts them, sets proper permissions, and restarts the service.

## Version History

### v1.0.13 (Latest)
- **Improved UI layout**: Made extension editor and PJSIP editor much wider
- Changed from constrained layout to full-width fluid container
- Extension editor now uses col-lg-2/10 (was col-md-3/9) for better space utilization
- PJSIP text editor now spans full available width
- Better use of screen real estate on large displays

### v1.0.12
- **Automatic dialplan management**: Extensions are now automatically added to extensions.conf
- When adding an extension, a dialplan entry is created in [from-internal] context
- Format: `exten => {extension},1,Dial(PJSIP/{extension},20)` with Hangup()
- Works for both web UI and MQTT-triggered extension additions
- Automatic cleanup when extensions are deleted
- Created ExtensionsConfManagementService for dialplan management

### v1.0.11
- Fixed TestUpdate page service injection error
- Application now properly retrieves AutoUpdateService from hosted services
- Improved error handling for manual update triggers

### v1.0.10
- Added log level toggle button to SystemLogs page
- Created LogLevelService for dynamic log level management
- Added 10-second startup delay for MQTT service to allow network to settle
- Added local IP address to HEARTBEAT message payload
- Improved production troubleshooting capabilities

### v1.0.9
- Fixed log display formatting - added line breaks between log entries
- Improved log readability with white-space preservation

### v1.0.8
- Fixed interactive features in SystemLogs, TestUpdate, and AsteriskRestart pages
- Added @rendermode InteractiveServer directive for proper state management

### v1.0.7
- Changed application port from 5000 to 8080
- Updated documentation for new port
- Verified auto-start on reboot functionality

### v1.0.6
- Added TestUpdate page for manual update testing via web UI

### v1.0.5
- Fixed update script file permissions
- Improved update.sh with explicit chmod +x commands

### v1.0.4
- Fixed critical auto-update bug with Unix line endings
- Changed from Windows CRLF to Unix LF for bash scripts

### v1.0.1
- MQTT-triggered software updates
- Remote update via `cmnd/UBI/{MacAddress}/SIPCMD/UPDATE` topic
- Enhanced logging for update operations
- Network binding fix (listen on all interfaces)

### v1.0.0 (Initial Release)
- Blazor Server web interface
- PJSIP extension management
- Asterisk configuration editing
- Log viewer
- MQTT integration with heartbeat
- Auto-update functionality
- WiFi management
- Systemd service integration
