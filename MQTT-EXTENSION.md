# MQTT Extension Management

This document describes how to add or modify PJSIP extensions via MQTT commands.

## Overview

The MQTT service listens for extension management commands on the topic:
```
cmnd/UBI/{MacAddress}/SIPCMD/EXTENSION
```

When a message is received on this topic, the system will:
1. Parse the JSON payload containing extension data
2. Check if the extension exists
3. Add or modify the extension in the PJSIP configuration
4. Save the configuration file
5. Reload PJSIP to apply changes

## Payload Format

The payload must be a JSON object containing the extension properties:

### Required Fields
- `Name` (string): The extension name/number

### Optional Fields

#### Endpoint Configuration
- `Context` (string): Dialplan context (default: "from-internal")
- `Disallow` (string): Codecs to disallow (default: "all")
- `Allow` (string): Codecs to allow (default: "ulaw")
- `RtpSymmetric` (bool): Enable symmetric RTP (default: true)
- `RewriteContact` (bool): Rewrite contact header (default: true)
- `ForceRport` (bool): Force rport (default: true)
- `EndpointExtra` (object): Additional endpoint properties

#### Authentication Configuration
- `AuthType` (string): Authentication type (default: "userpass")
- `Username` (string): Authentication username (default: extension name)
- `Password` (string): Authentication password
- `AuthExtra` (object): Additional auth properties

#### AOR Configuration
- `MaxContacts` (int): Maximum contacts (default: 1)
- `AorExtra` (object): Additional AOR properties

## Examples

### Add a Simple Extension

```bash
mosquitto_pub -h mqtt.jsmplus.com -p 4546 \
  -u "1C54E630570D" -P "UBIPASS" \
  -t "cmnd/UBI/1C54E630570D/SIPCMD/EXTENSION" \
  -m '{
    "Name": "100",
    "Password": "secret123",
    "Username": "100"
  }'
```

### Add Extension with Custom Codecs

```bash
mosquitto_pub -h mqtt.jsmplus.com -p 4546 \
  -u "1C54E630570D" -P "UBIPASS" \
  -t "cmnd/UBI/1C54E630570D/SIPCMD/EXTENSION" \
  -m '{
    "Name": "101",
    "Password": "mypassword",
    "Username": "101",
    "Allow": "ulaw,alaw,g722",
    "Context": "from-internal"
  }'
```

### Modify Existing Extension

To modify an extension, send the same command with the same `Name` but different properties:

```bash
mosquitto_pub -h mqtt.jsmplus.com -p 4546 \
  -u "1C54E630570D" -P "UBIPASS" \
  -t "cmnd/UBI/1C54E630570D/SIPCMD/EXTENSION" \
  -m '{
    "Name": "100",
    "Password": "newsecret456",
    "Allow": "ulaw,alaw"
  }'
```

### Add Extension with Multiple Contacts

```bash
mosquitto_pub -h mqtt.jsmplus.com -p 4546 \
  -u "1C54E630570D" -P "UBIPASS" \
  -t "cmnd/UBI/1C54E630570D/SIPCMD/EXTENSION" \
  -m '{
    "Name": "102",
    "Password": "pass102",
    "MaxContacts": 5
  }'
```

## Complete Extension Example

```json
{
  "Name": "200",
  "Context": "from-internal",
  "Disallow": "all",
  "Allow": "ulaw,alaw,g722",
  "RtpSymmetric": true,
  "RewriteContact": true,
  "ForceRport": true,
  "AuthType": "userpass",
  "Username": "200",
  "Password": "supersecret",
  "MaxContacts": 1,
  "EndpointExtra": {
    "callerid": "Extension 200 <200>",
    "direct_media": "no"
  },
  "AuthExtra": {},
  "AorExtra": {
    "qualify_frequency": "60"
  }
}
```

## Monitoring

Check the application logs to verify extension processing:

```bash
# View real-time logs
sudo journalctl -u asteriskmanager -f

# View recent logs
sudo journalctl -u asteriskmanager -n 100
```

Look for log messages like:
- "Received EXTENSION command via MQTT"
- "Adding new extension: {name}" or "Modifying existing extension: {name}"
- "Extension {name} processed successfully"

## Troubleshooting

### Invalid JSON Payload
If the JSON is malformed, you'll see:
```
Error parsing extension JSON payload
```
**Solution**: Validate your JSON syntax before sending.

### Missing Extension Name
If the `Name` field is missing or empty:
```
Invalid extension data received. Extension name is required.
```
**Solution**: Ensure the `Name` field is included in your payload.

### PJSIP Reload Failed
If PJSIP fails to reload:
```
Extension {name} processed successfully. Reload result: [error message]
```
**Solution**: Check Asterisk logs for configuration errors:
```bash
sudo asterisk -rx "pjsip show endpoints"
```

## Testing

You can verify the extension was added by:

1. **Check the PJSIP configuration file:**
   ```bash
   cat /etc/asterisk/pjsip.conf
   ```

2. **View endpoints in Asterisk:**
   ```bash
   sudo asterisk -rx "pjsip show endpoints"
   ```

3. **Check specific endpoint:**
   ```bash
   sudo asterisk -rx "pjsip show endpoint 100"
   ```

## Integration

You can integrate this feature into your management system by:

1. Collecting extension data from a web form or API
2. Converting it to JSON format
3. Publishing to the MQTT topic
4. Monitoring the logs for confirmation

Example using Python with paho-mqtt:

```python
import json
import paho.mqtt.client as mqtt

# Extension data
extension = {
    "Name": "100",
    "Password": "secret123",
    "Username": "100",
    "Allow": "ulaw,alaw"
}

# MQTT configuration
broker = "mqtt.jsmplus.com"
port = 4546
mac_address = "1C54E630570D"
password = "UBIPASS"

# Create client and publish
client = mqtt.Client()
client.username_pw_set(mac_address, password)
client.connect(broker, port)

topic = f"cmnd/UBI/{mac_address}/SIPCMD/EXTENSION"
payload = json.dumps(extension)

client.publish(topic, payload)
client.disconnect()

print(f"Extension {extension['Name']} sent successfully")
```

## Notes

- The extension name should be unique
- Password is required for authentication
- Changes take effect immediately after PJSIP reload
- Default values are used for any omitted optional fields
- The system will automatically create all three PJSIP sections: endpoint, auth, and aor
