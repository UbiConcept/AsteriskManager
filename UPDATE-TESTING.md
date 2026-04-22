# Testing Auto-Update Functionality

This guide explains how to test the auto-update feature of Asterisk Manager.

## Prerequisites

- ✅ `update.zip` package created
- ✅ Device accessible at 192.168.18.39
- ✅ SSH access (linaro/linaro)
- ✅ MQTT broker accessible (mqtt.jsmplus.com:4546)

## Step 1: Create Update Package

Run the script to create `update.zip`:

```powershell
.\create-update-package.ps1
```

This will:
- Build the application for linux-arm64
- Package all necessary files
- Create `update.zip` (~44 MB)

## Step 2: Choose a Testing Method

### Method A: Automated Test (Recommended)

Use the automated test script:

```powershell
.\test-update.ps1
```

This script will:
1. Upload `update.zip` to the device
2. Start a local HTTP server on the device (port 8080)
3. Update configuration to use local server
4. Trigger update via MQTT
5. Monitor logs in real-time

### Method B: Manual Test with Local HTTP Server

1. **Upload update.zip to device:**
   ```powershell
   pscp -pw linaro update.zip linaro@192.168.18.39:/tmp/
   ```

2. **SSH into device:**
   ```powershell
   plink -ssh -pw linaro linaro@192.168.18.39
   ```

3. **Start local HTTP server:**
   ```bash
   cd /tmp
   python3 -m http.server 8080 &
   ```

4. **Update configuration:**
   ```bash
   sudo nano /opt/asteriskmanager/appsettings.json
   ```
   
   Change:
   ```json
   "UpdateUrl": "http://127.0.0.1:8080"
   ```

5. **Restart service:**
   ```bash
   sudo systemctl restart asteriskmanager
   ```

6. **Trigger update via MQTT:**
   ```bash
   mosquitto_pub -h mqtt.jsmplus.com -p 4546 \
     -u 1C54E63057D0 -P UBIPASS \
     -t "cmnd/UBI/1C54E63057D0/SIPCMD/UPDATE" \
     -m "update"
   ```

7. **Monitor logs:**
   ```bash
   sudo journalctl -u asteriskmanager -f
   ```

### Method C: Test with External Web Server

1. **Upload update.zip to a web server** (e.g., www.ubiconcept.com)

2. **Update appsettings.json on device:**
   ```json
   "UpdateUrl": "https://www.ubiconcept.com"
   ```

3. **Trigger update via MQTT:**
   ```bash
   mosquitto_pub -h mqtt.jsmplus.com -p 4546 \
     -u 1C54E63057D0 -P UBIPASS \
     -t "cmnd/UBI/1C54E63057D0/SIPCMD/UPDATE" \
     -m "update"
   ```

### Method D: Manual Extraction Test

For quick verification without update mechanism:

```powershell
.\test-update-manual.ps1
```

Then SSH and manually copy files:

```bash
ssh linaro@192.168.18.39
cd /tmp/update-test
sudo systemctl stop asteriskmanager
sudo cp -r * /opt/asteriskmanager/
sudo chmod +x /opt/asteriskmanager/AsteriskManager
sudo systemctl start asteriskmanager
```

## What to Look For in Logs

### Successful Update Logs:

```
info: AsteriskManager.Services.MqttService[0]
      Received UPDATE command via MQTT. Initiating software update...

info: AsteriskManager.Services.AutoUpdateService[0]
      Checking for updates from http://127.0.0.1:8080/update.zip

info: AsteriskManager.Services.AutoUpdateService[0]
      Update downloaded successfully. Size: 46234567 bytes

info: AsteriskManager.Services.AutoUpdateService[0]
      Extracting update package...

info: AsteriskManager.Services.AutoUpdateService[0]
      Applying update to /opt/asteriskmanager

info: AsteriskManager.Services.AutoUpdateService[0]
      Update applied successfully. Restarting application...
```

### After Restart:

```
info: AsteriskManager.Services.MqttService[0]
      Starting MQTT service with MAC address: 1C54E63057D0

info: Microsoft.Hosting.Lifetime[0]
      Now listening on: http://0.0.0.0:5000
```

## Troubleshooting

### Update Download Fails

**Check:**
```bash
# Test URL accessibility
curl -I http://127.0.0.1:8080/update.zip

# Check HTTP server
ps aux | grep python
netstat -tulpn | grep 8080
```

### Update Extraction Fails

**Check:**
```bash
# Verify ZIP file
unzip -t /tmp/update.zip

# Check disk space
df -h /
```

### Service Won't Start After Update

**Check logs:**
```bash
sudo journalctl -u asteriskmanager -n 100 --no-pager
```

**Common issues:**
- Port 5000 already in use
- Missing dependencies
- Permission issues

**Fix:**
```bash
# Kill old processes
sudo killall -9 AsteriskManager

# Check permissions
sudo chmod +x /opt/asteriskmanager/AsteriskManager
sudo chown -R linaro:linaro /opt/asteriskmanager

# Restart
sudo systemctl restart asteriskmanager
```

## Restore from Backup

If update fails, restore backup:

```bash
# Stop service
sudo systemctl stop asteriskmanager

# Restore from backup (if created)
sudo cp /opt/asteriskmanager/appsettings.json.backup /opt/asteriskmanager/appsettings.json

# Or re-deploy previous version
# (upload previous version files)

# Start service
sudo systemctl start asteriskmanager
```

## Verification

After successful update, verify:

1. **Service is running:**
   ```bash
   sudo systemctl status asteriskmanager
   ```

2. **Web interface accessible:**
   ```
   http://192.168.18.39:5000
   ```

3. **MQTT connected:**
   ```bash
   sudo journalctl -u asteriskmanager -n 20 --no-pager | grep -i mqtt
   ```

4. **Check version in heartbeat:**
   Monitor MQTT topic `tele/UBI/1C54E63057D0/HEARTBEAT` for version info

## Automated Update Schedule

The service checks for updates every 60 minutes automatically (configurable in `appsettings.json`):

```json
"AutoUpdate": {
  "UpdateUrl": "https://www.ubiconcept.com",
  "Enabled": true,
  "CheckIntervalMinutes": 60
}
```

## Next Steps

Once testing is successful:

1. ✅ Upload final `update.zip` to production server
2. ✅ Update production `appsettings.json` with correct URL
3. ✅ Test MQTT-triggered updates
4. ✅ Monitor automatic update checks
5. ✅ Document rollback procedures

---

## Quick Reference Commands

```powershell
# Create package
.\create-update-package.ps1

# Automated test
.\test-update.ps1

# Manual setup
.\test-update-manual.ps1

# Monitor logs
plink -ssh -pw linaro linaro@192.168.18.39 "sudo journalctl -u asteriskmanager -f"

# Trigger update via MQTT
mosquitto_pub -h mqtt.jsmplus.com -p 4546 -u 1C54E63057D0 -P UBIPASS -t "cmnd/UBI/1C54E63057D0/SIPCMD/UPDATE" -m "update"
```
