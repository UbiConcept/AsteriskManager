namespace AsteriskManager.Services;

public class AutoSshService
{
    private readonly ILogger<AutoSshService> _logger;
    private readonly string _configPath;
    private int? _remotePort;

    public AutoSshService(ILogger<AutoSshService> logger)
    {
        _logger = logger;

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

    public async Task SetRemotePortAsync(int port)
    {
        if (port <= 0 || port > 65535)
        {
            throw new ArgumentException($"Invalid port number: {port}. Port must be between 1 and 65535.", nameof(port));
        }

        RemotePort = port;
        await SaveConfigurationAsync();

        _logger.LogInformation("Remote port configuration saved: {Port}", port);
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
            var content = $"# AutoSSH Configuration\nREMOTE_PORT={_remotePort}\n";
            await File.WriteAllTextAsync(_configPath, content);

            _logger.LogInformation("AutoSSH configuration saved to {Path}", _configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving AutoSSH configuration");
            throw;
        }
    }

    public string GetConfigurationSummary()
    {
        if (_remotePort.HasValue)
        {
            return $"Remote Port: {_remotePort}";
        }

        return "Remote port not configured";
    }
}
