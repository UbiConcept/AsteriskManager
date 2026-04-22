@echo off
echo ========================================
echo Asterisk Manager Update
echo ========================================
echo Target: linaro@192.168.18.39
echo.

echo [1/3] Uploading update package...
scp asteriskmanager-update.tar.gz linaro@192.168.18.39:/tmp/
if errorlevel 1 (
    echo ERROR: Upload failed!
    pause
    exit /b 1
)

echo.
echo [2/3] Stopping old service and extracting update...
ssh linaro@192.168.18.39 "sudo systemctl stop asteriskmanager; sudo pkill -9 AsteriskManager; sleep 2; cd /tmp && mkdir -p asteriskmanager-temp && tar -xzf asteriskmanager-update.tar.gz -C asteriskmanager-temp"
if errorlevel 1 (
    echo ERROR: Stop/Extract failed!
    pause
    exit /b 1
)

echo.
echo [3/3] Installing update and starting service...
ssh linaro@192.168.18.39 "cd /tmp/asteriskmanager-temp && sudo rm -rf /opt/asteriskmanager/* && sudo cp -r * /opt/asteriskmanager/ && sudo chmod +x /opt/asteriskmanager/AsteriskManager && sudo systemctl daemon-reload && sudo systemctl start asteriskmanager && sleep 3 && sudo systemctl status asteriskmanager --no-pager && cd /tmp && rm -rf asteriskmanager-temp asteriskmanager-update.tar.gz"

echo.
echo ========================================
echo Update Complete!
echo ========================================
echo.
echo Access: http://192.168.18.39:5000
echo.
pause
