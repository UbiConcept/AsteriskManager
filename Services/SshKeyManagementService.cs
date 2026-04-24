using System.Diagnostics;

namespace AsteriskManager.Services;

public class SshKeyManagementService : IHostedService
{
    private readonly ILogger<SshKeyManagementService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _sshKeyPath;
    private readonly string _sshDirectory;

    public SshKeyManagementService(
        ILogger<SshKeyManagementService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

        var appPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        _sshDirectory = Path.Combine(appPath!, "ssh");
        _sshKeyPath = Path.Combine(_sshDirectory, "id_rsa");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking SSH key configuration...");

        try
        {
            // Create ssh directory if it doesn't exist
            if (!Directory.Exists(_sshDirectory))
            {
                _logger.LogInformation("Creating SSH directory: {Path}", _sshDirectory);
                Directory.CreateDirectory(_sshDirectory);

                // Set directory permissions to 700 (owner only)
                await SetPermissionsAsync(_sshDirectory, "700", cancellationToken);
            }

            // Check if SSH key exists
            if (!File.Exists(_sshKeyPath))
            {
                _logger.LogWarning("SSH key not found. Generating new SSH key pair...");
                await GenerateSshKeyAsync(cancellationToken);
            }
            else
            {
                _logger.LogInformation("SSH key found at {Path}", _sshKeyPath);

                // Ensure correct permissions on existing key
                await SetPermissionsAsync(_sshKeyPath, "600", cancellationToken);
                await SetPermissionsAsync($"{_sshKeyPath}.pub", "644", cancellationToken);
            }

            // Display public key for reference
            if (File.Exists($"{_sshKeyPath}.pub"))
            {
                var publicKey = await File.ReadAllTextAsync($"{_sshKeyPath}.pub", cancellationToken);
                _logger.LogInformation("SSH Public Key:\n{PublicKey}", publicKey.Trim());

                // Publish public key via MQTT
                _ = Task.Run(async () => await PublishPublicKeyAsync(publicKey.Trim()), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SSH key");
        }

        return;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task GenerateSshKeyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ssh-keygen",
                Arguments = $"-t rsa -b 4096 -f \"{_sshKeyPath}\" -N \"\" -C \"asteriskmanager@autossh\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);

            if (process == null)
            {
                throw new Exception("Failed to start ssh-keygen process");
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                throw new Exception($"ssh-keygen failed with exit code {process.ExitCode}: {error}");
            }

            _logger.LogInformation("SSH key pair generated successfully");
            _logger.LogInformation("ssh-keygen output: {Output}", output);

            // Set proper permissions on the generated keys
            await SetPermissionsAsync(_sshKeyPath, "600", cancellationToken);
            await SetPermissionsAsync($"{_sshKeyPath}.pub", "644", cancellationToken);

            // Read and log the public key
            if (File.Exists($"{_sshKeyPath}.pub"))
            {
                var publicKey = await File.ReadAllTextAsync($"{_sshKeyPath}.pub", cancellationToken);
                _logger.LogInformation("Generated SSH Public Key:\n{PublicKey}", publicKey.Trim());
                _logger.LogInformation("Add this public key to the remote server's authorized_keys file for autossh access");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate SSH key");
            throw;
        }
    }

    private async Task SetPermissionsAsync(string path, string permissions, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"{permissions} \"{path}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);

            if (process == null)
            {
                _logger.LogWarning("Failed to start chmod process for {Path}", path);
                return;
            }

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0)
            {
                _logger.LogDebug("Set permissions {Permissions} on {Path}", permissions, path);
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogWarning("Failed to set permissions on {Path}: {Error}", path, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting permissions on {Path}", path);
        }
    }

    public string GetPublicKey()
    {
        var publicKeyPath = $"{_sshKeyPath}.pub";

        if (File.Exists(publicKeyPath))
        {
            return File.ReadAllText(publicKeyPath).Trim();
        }

        return string.Empty;
    }

    public string GetPrivateKeyPath()
    {
        return _sshKeyPath;
    }

    public string GetSshDirectory()
    {
        return _sshDirectory;
    }

    private async Task PublishPublicKeyAsync(string publicKey)
    {
        try
        {
            // Wait for MQTT service to be ready (it has a 10-second startup delay)
            await Task.Delay(TimeSpan.FromSeconds(15));

            var mqttService = _serviceProvider.GetServices<IHostedService>()
                .OfType<MqttService>()
                .FirstOrDefault();

            if (mqttService == null)
            {
                _logger.LogWarning("MqttService not found. Cannot publish SSH public key.");
                return;
            }

            var macAddress = mqttService.GetMacAddressValue();

            if (string.IsNullOrEmpty(macAddress))
            {
                _logger.LogWarning("MAC address not available. Cannot publish SSH public key.");
                return;
            }

            var topic = $"tele/UBI/{macAddress}/PUBLICKEY";

            _logger.LogInformation("Publishing SSH public key to MQTT topic: {Topic}", topic);
            await mqttService.PublishMessageAsync(topic, publicKey);
            _logger.LogInformation("SSH public key published successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish SSH public key via MQTT");
        }
    }
}

