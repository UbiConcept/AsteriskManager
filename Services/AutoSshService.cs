using System.Diagnostics;

namespace AsteriskManager.Services;

public class AutoSshService
{
    private readonly ILogger<AutoSshService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _configPath;
    private readonly string _systemdServicePath = "/etc/systemd/system/autossh-tunnel.service";
    private int? _remotePort;
    private string? _remoteHost;

    public AutoSshService(ILogger<AutoSshService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

        var appPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        _configPath = Path.Combine(appPath!, "autossh.conf");

        // Load configuration on startup
        LoadConfiguration();
    }

    public int? RemotePort
    {
        get => _remotePort;
        private set
        {
            _remotePort = value;
            _logger.LogInformation("Remote port set to: {Port}", value);
        }
    }

    public string? RemoteHost
    {
        get => _remoteHost;
        private set
        {
            _remoteHost = value;
            _logger.LogInformation("Remote host set to: {Host}", value);
        }
    }

    public async Task SetRemotePortAsync(int port, string remoteHost = "ubiconcept.com")
    {
        if (port <= 0 || port > 65535)
        {
            throw new ArgumentException($"Invalid port number: {port}. Port must be between 1 and 65535.", nameof(port));
        }

        RemotePort = port;
        RemoteHost = remoteHost;
        await SaveConfigurationAsync();

        _logger.LogInformation("Remote port configuration saved: {Port} for host {Host}", port, remoteHost);

        // Configure and start autossh
        await ConfigureAutoSshAsync();
    }

    private async Task ConfigureAutoSshAsync()
    {
        try
        {
            if (!_remotePort.HasValue || string.IsNullOrEmpty(_remoteHost))
            {
                _logger.LogWarning("Cannot configure autossh: Remote port or host not set");
                return;
            }

            // Get SSH key path from SshKeyManagementService
            var sshKeyService = _serviceProvider.GetRequiredService<SshKeyManagementService>();
            var privateKeyPath = sshKeyService.GetPrivateKeyPath();

            if (!File.Exists(privateKeyPath))
            {
                _logger.LogError("SSH private key not found at {Path}. Cannot configure autossh.", privateKeyPath);
                return;
            }

            _logger.LogInformation("Configuring autossh tunnel: {RemoteHost}:{RemotePort} -> localhost:22", _remoteHost, _remotePort);

            // Create systemd service file
            await CreateSystemdServiceAsync(privateKeyPath);

            // Reload systemd daemon
            await RunCommandAsync("systemctl", "daemon-reload", "Reloading systemd daemon");

            // Enable service to start on boot
            await RunCommandAsync("systemctl", "enable autossh-tunnel.service", "Enabling autossh service");

            // Restart service
            await RunCommandAsync("systemctl", "restart autossh-tunnel.service", "Starting autossh service");

            // Check service status
            await Task.Delay(2000);
            await RunCommandAsync("systemctl", "status autossh-tunnel.service --no-pager", "Checking autossh service status");

            _logger.LogInformation("AutoSSH tunnel configured and started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring autossh");
        }
    }

    private async Task CreateSystemdServiceAsync(string privateKeyPath)
    {
        var serviceContent = $"""
            [Unit]
            Description=AutoSSH Reverse Tunnel
            After=network-online.target
            Wants=network-online.target

            [Service]
            Type=simple
            User=root
            Environment="AUTOSSH_GATETIME=0"
            Environment="AUTOSSH_PORT=0"
            ExecStart=/usr/bin/autossh -M 0 -N -o "ServerAliveInterval 30" -o "ServerAliveCountMax 3" -o "StrictHostKeyChecking=no" -o "UserKnownHostsFile=/dev/null" -i "{privateKeyPath}" -R {_remotePort}:localhost:22 root@{_remoteHost}
            Restart=always
            RestartSec=10

            [Install]
            WantedBy=multi-user.target
            """;

        try
        {
            // Write service file using a temporary file first
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, serviceContent);

            // Copy to systemd location with sudo
            var copyResult = await RunCommandAsync("cp", $"{tempFile} {_systemdServicePath}", "Creating systemd service file");

            // Clean up temp file
            File.Delete(tempFile);

            // Set proper permissions
            await RunCommandAsync("chmod", $"644 {_systemdServicePath}", "Setting service file permissions");

            _logger.LogInformation("Created systemd service file at {Path}", _systemdServicePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating systemd service file");
            throw;
        }
    }

    private async Task<bool> RunCommandAsync(string command, string arguments, string description)
    {
        try
        {
            _logger.LogInformation("{Description}: {Command} {Arguments}", description, command, arguments);

            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);

            if (process == null)
            {
                _logger.LogWarning("Failed to start process: {Command}", command);
                return false;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (!string.IsNullOrWhiteSpace(output))
            {
                _logger.LogInformation("Command output: {Output}", output);
            }

            if (!string.IsNullOrWhiteSpace(error) && process.ExitCode != 0)
            {
                _logger.LogWarning("Command error: {Error}", error);
            }

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running command: {Command} {Arguments}", command, arguments);
            return false;
        }
    }

    private void LoadConfiguration()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var content = File.ReadAllText(_configPath);
                var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("REMOTE_PORT=", StringComparison.OrdinalIgnoreCase))
                    {
                        var portString = trimmedLine.Substring("REMOTE_PORT=".Length);
                        if (int.TryParse(portString, out var port))
                        {
                            _remotePort = port;
                            _logger.LogInformation("Loaded remote port from configuration: {Port}", port);
                        }
                    }
                    else if (trimmedLine.StartsWith("REMOTE_HOST=", StringComparison.OrdinalIgnoreCase))
                    {
                        _remoteHost = trimmedLine.Substring("REMOTE_HOST=".Length).Trim();
                        _logger.LogInformation("Loaded remote host from configuration: {Host}", _remoteHost);
                    }
                }

                // If configuration is loaded, reconfigure autossh
                if (_remotePort.HasValue && !string.IsNullOrEmpty(_remoteHost))
                {
                    _ = Task.Run(async () => await ConfigureAutoSshAsync());
                }
            }
            else
            {
                _logger.LogInformation("AutoSSH configuration file not found. Will be created when remote port is set.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading AutoSSH configuration");
        }
    }

    private async Task SaveConfigurationAsync()
    {
        try
        {
            var content = $"# AutoSSH Configuration\nREMOTE_PORT={_remotePort}\nREMOTE_HOST={_remoteHost}\n";
            await File.WriteAllTextAsync(_configPath, content);

            _logger.LogInformation("AutoSSH configuration saved to {Path}", _configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving AutoSSH configuration");
            throw;
        }
    }

    public async Task StopAutoSshAsync()
    {
        try
        {
            _logger.LogInformation("Stopping autossh service");
            await RunCommandAsync("systemctl", "stop autossh-tunnel.service", "Stopping autossh service");
            await RunCommandAsync("systemctl", "disable autossh-tunnel.service", "Disabling autossh service");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping autossh");
        }
    }

    public string GetConfigurationSummary()
    {
        if (_remotePort.HasValue && !string.IsNullOrEmpty(_remoteHost))
        {
            return $"Remote Host: {_remoteHost}, Remote Port: {_remotePort} -> localhost:22";
        }

        return "AutoSSH not configured";
    }
}
