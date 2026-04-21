# Asterisk Manager

A Blazor Server application for managing Asterisk PBX configurations with MQTT integration and auto-update capabilities.

## Version

**Current Version:** 1.0.0

## Features

- 🌐 **Blazor Server Web Interface** - Modern, responsive web UI for Asterisk management
- 📞 **PJSIP Extension Management** - Create, edit, and manage SIP extensions
- 📝 **Configuration Editor** - Direct editing of Asterisk configuration files
- 📊 **Log Viewer** - Real-time viewing of Asterisk logs
- 📡 **MQTT Integration** - Remote command and control via MQTT
  - Subscribes to: `cmnd/UBI/{MacAddress}/SIPCMD/#`
  - Heartbeat: `tele/UBI/{MacAddress}/HEARTBEAT` (every 60 seconds)
  - **Remote Updates**: Trigger software updates via MQTT command (see [MQTT-UPDATE.md](MQTT-UPDATE.md))
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

Access the web interface at `http://<device-ip>:5000`

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

## Version History

### v1.0.1 (Latest)
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
