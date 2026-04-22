using System.Diagnostics;
using System.IO.Compression;
using System.Net.NetworkInformation;
using System.Reflection;
using Microsoft.Extensions.Options;

namespace AsteriskManager.Services;

public class AutoUpdateService : BackgroundService
{
    private readonly ILogger<AutoUpdateService> _logger;
    private readonly AutoUpdateSettings _settings;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly HttpClient _httpClient;
    private string? _macAddress;

    public AutoUpdateService(
        ILogger<AutoUpdateService> logger,
        IOptions<AutoUpdateSettings> settings,
        IHostApplicationLifetime lifetime,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _settings = settings.Value;
        _lifetime = lifetime;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(10);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Auto-update is disabled");
            return;
        }

        _macAddress = GetMacAddress();

        if (string.IsNullOrEmpty(_macAddress))
        {
            _logger.LogWarning("Could not determine MAC address for auto-update");
            return;
        }

        _logger.LogInformation("Auto-update service started. Checking every {Interval} minutes", _settings.CheckIntervalMinutes);

        using var updateTimer = new PeriodicTimer(TimeSpan.FromMinutes(_settings.CheckIntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await updateTimer.WaitForNextTickAsync(stoppingToken);
                await CheckAndApplyUpdateAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during update check");
            }
        }
    }

    public async Task CheckAndApplyUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Checking for updates from {Url}", _settings.UpdateUrl);

            var updatePackagePath = await DownloadUpdateAsync(cancellationToken);

            if (string.IsNullOrEmpty(updatePackagePath))
            {
                _logger.LogInformation("No update available");
                return;
            }

            _logger.LogInformation("Update downloaded. Applying update...");

            await ApplyUpdateAsync(updatePackagePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check or apply update");
        }
    }

    private async Task<string?> DownloadUpdateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var updateUrl = $"{_settings.UpdateUrl.TrimEnd('/')}/update.zip";
            
            _logger.LogInformation("Downloading update from {Url}", updateUrl);

            var response = await _httpClient.GetAsync(updateUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Update not available. Status: {StatusCode}", response.StatusCode);
                return null;
            }

            var tempPath = Path.Combine(Path.GetTempPath(), $"update_{Guid.NewGuid()}.zip");

            await using (var fileStream = File.Create(tempPath))
            {
                await response.Content.CopyToAsync(fileStream, cancellationToken);
            }

            _logger.LogInformation("Update package downloaded to {Path}", tempPath);
            return tempPath;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not download update");
            return null;
        }
    }

    private async Task ApplyUpdateAsync(string updatePackagePath, CancellationToken cancellationToken)
    {
        try
        {
            var currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            
            if (string.IsNullOrEmpty(currentPath))
            {
                _logger.LogError("Could not determine current application path");
                return;
            }

            var updatePath = Path.Combine(Path.GetTempPath(), $"update_{Guid.NewGuid()}");
            Directory.CreateDirectory(updatePath);

            _logger.LogInformation("Extracting update to {Path}", updatePath);
            ZipFile.ExtractToDirectory(updatePackagePath, updatePath, overwriteFiles: true);

            var updateScriptPath = Path.Combine(currentPath, "update.sh");

            var scriptContent = $"""
                #!/bin/bash
                sleep 3
                echo "Applying update..."
                cp -rf "{updatePath}"/* "{currentPath}"/
                chmod +x "{currentPath}/AsteriskManager"
                chmod 644 "{currentPath}"/*.json
                chmod 644 "{currentPath}"/*.dll
                rm -rf "{updatePath}"
                rm -f "{updatePackagePath}"
                echo "Update applied. Restarting application..."
                systemctl restart asteriskmanager
                rm -f "$0"
                """;

            // Write with Unix line endings (LF only)
            var scriptBytes = System.Text.Encoding.UTF8.GetBytes(scriptContent.Replace("\r\n", "\n").Replace("\r", "\n"));
            await File.WriteAllBytesAsync(updateScriptPath, scriptBytes, cancellationToken);

            var chmod = Process.Start(new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{updateScriptPath}\"",
                UseShellExecute = false
            });
            await chmod!.WaitForExitAsync(cancellationToken);

            _logger.LogInformation("Starting update script and stopping application");

            _ = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"\"{updateScriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            await Task.Delay(1000, cancellationToken);
            
            _lifetime.StopApplication();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply update");
        }
    }

    private static string? GetMacAddress()
    {
        var nics = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                     && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .OrderBy(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 0 : 1)
            .ToList();

        if (nics.Count == 0)
            return null;

        var mac = nics[0].GetPhysicalAddress().ToString();

        if (string.IsNullOrEmpty(mac))
            return null;

        // Return MAC address without colons
        return mac;
    }
}
