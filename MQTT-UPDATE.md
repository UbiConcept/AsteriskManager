# MQTT-Triggered Software Update Feature

## Overview
The Asterisk Manager application now supports remote software updates triggered via MQTT commands.

## How It Works

### MQTT Topic
The application listens for update commands on:
```
cmnd/UBI/{MacAddress}/SIPCMD/UPDATE
```

Where `{MacAddress}` is automatically detected from the device's network interface.

### Trigger an Update
To trigger a software update remotely, publish any message to the UPDATE topic:

**Using mosquitto_pub:**
```bash
mosquitto_pub -h mqtt.jsmplus.com -p 4546 -u {MacAddress} -P UBIPASS \
  -t "cmnd/UBI/{MacAddress}/SIPCMD/UPDATE" -m "update"
```

**Example with actual MAC address:**
```bash
mosquitto_pub -h mqtt.jsmplus.com -p 4546 -u 1C54E630570D -P UBIPASS \
  -t "cmnd/UBI/1C54E630570D/SIPCMD/UPDATE" -m "update"
```

## Update Process

When an UPDATE command is received:

1. **MQTT Service** receives the message on the subscribed topic
2. **Validation** - Checks if topic ends with `/SIPCMD/UPDATE`
3. **Logging** - Logs the update initiation
4. **Trigger** - Calls `AutoUpdateService.CheckAndApplyUpdateAsync()`
5. **Download** - Downloads update from `https://www.ubiconcept.com/update.zip`
6. **Extract** - Extracts update files to temporary location
7. **Apply** - Copies new files to `/opt/asteriskmanager/`
8. **Restart** - Restarts the application via systemd

## Update Package Requirements

The update package must be:
- **Location**: `https://www.ubiconcept.com/update.zip`
- **Format**: ZIP archive
- **Contents**: Complete application files matching the deployment structure
- **Permissions**: Must be accessible via HTTP/HTTPS

## Logging

All update activities are logged and can be viewed:
```bash
# View update logs in real-time
sudo journalctl -u asteriskmanager -f

# View recent update logs
sudo journalctl -u asteriskmanager -n 100 --no-pager
```

## Security Considerations

1. **MQTT Authentication**: Requires MAC address as username and configured password
2. **HTTPS**: Update downloads use HTTPS (if configured)
3. **Automatic Restart**: Application automatically restarts after update
4. **Rollback**: Manual rollback required if update fails

## Configuration

Update behavior is configured in `appsettings.json`:

```json
{
  "AutoUpdate": {
    "UpdateUrl": "https://www.ubiconcept.com",
    "Enabled": true,
    "CheckIntervalMinutes": 60
  }
}
```

- **UpdateUrl**: Base URL for update downloads
- **Enabled**: Enable/disable automatic updates
- **CheckIntervalMinutes**: Interval for automatic update checks (separate from MQTT-triggered updates)

## Testing

To test the MQTT update trigger:

1. Ensure MQTT broker is accessible
2. Device must be connected and subscribed
3. Publish to the UPDATE topic
4. Monitor logs for update process
5. Verify application restarts with new version

## Troubleshooting

**Update not triggering:**
- Check MQTT connection: `sudo systemctl status asteriskmanager`
- Verify MAC address in topic matches device
- Check MQTT credentials

**Update fails:**
- Verify `update.zip` exists at configured URL
- Check disk space: `df -h /`
- Review logs: `sudo journalctl -u asteriskmanager -n 100`

**Application doesn't restart:**
- Check systemd service: `sudo systemctl status asteriskmanager`
- Manually restart: `sudo systemctl restart asteriskmanager`

## Version History

### v1.0.1
- ✅ Added MQTT-triggered software update functionality
- ✅ Subscribe to UPDATE command topic
- ✅ Integrated with AutoUpdateService
- ✅ Added logging for update triggers

### v1.0.0
- Initial release with manual update support
