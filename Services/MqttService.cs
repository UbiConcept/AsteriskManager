using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace AsteriskManager.Services;

public class MqttService : BackgroundService
{
    private readonly ILogger<MqttService> _logger;
    private readonly MqttSettings _settings;
    private readonly IServiceProvider _serviceProvider;
    private IMqttClient? _mqttClient;
    private string? _macAddress;

    public MqttService(
        ILogger<MqttService> logger, 
        IOptions<MqttSettings> settings,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _settings = settings.Value;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Add startup delay to allow network to settle
        _logger.LogInformation("Waiting 10 seconds for network to settle before starting MQTT service...");
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        _macAddress = GetMacAddress();

        if (string.IsNullOrEmpty(_macAddress))
        {
            _logger.LogError("Could not determine MAC address. MQTT service will not start.");
            return;
        }

        _logger.LogInformation("Starting MQTT service with MAC address: {MacAddress}", _macAddress);

        var factory = new MqttClientFactory();
        _mqttClient = factory.CreateMqttClient();

        _mqttClient.DisconnectedAsync += async e =>
        {
            _logger.LogWarning("MQTT disconnected: {Reason}", e.Reason);

            if (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

                try
                {
                    await ConnectAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reconnecting to MQTT broker");
                }
            }
        };

        _mqttClient.ApplicationMessageReceivedAsync += async e =>
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = e.ApplicationMessage.ConvertPayloadToString();

            _logger.LogInformation("MQTT message received - Topic: {Topic}, Payload: {Payload}", topic, payload);

            await HandleMessageAsync(topic, payload);
        };

        await ConnectAsync(stoppingToken);

        using var heartbeatTimer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await heartbeatTimer.WaitForNextTickAsync(stoppingToken);
                await PublishHeartbeatAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending heartbeat");
            }
        }
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_settings.Server, _settings.Port)
                .WithCredentials(_macAddress, _settings.Password)
                .WithCleanSession()
                .Build();

            var result = await _mqttClient!.ConnectAsync(options, cancellationToken);

            if (result.ResultCode == MqttClientConnectResultCode.Success)
            {
                _logger.LogInformation("Connected to MQTT broker at {Server}:{Port}", _settings.Server, _settings.Port);

                var topicFilter = new MqttTopicFilterBuilder()
                    .WithTopic($"cmnd/UBI/{_macAddress}/SIPCMD/#")
                    .Build();

                var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(topicFilter)
                    .Build();

                var subscribeResult = await _mqttClient.SubscribeAsync(subscribeOptions, cancellationToken);
                _logger.LogInformation("Subscribed to topic: cmnd/UBI/{MacAddress}/SIPCMD/#", _macAddress);
            }
            else
            {
                _logger.LogError("Failed to connect to MQTT broker. Result: {ResultCode}", result.ResultCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to MQTT broker");
            throw;
        }
    }

    private async Task HandleMessageAsync(string topic, string payload)
    {
        // Check if this is an UPDATE command
        if (topic.EndsWith("/SIPCMD/UPDATE", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Received UPDATE command via MQTT. Initiating software update...");

            try
            {
                // Get the AutoUpdateService from the service provider
                var autoUpdateService = _serviceProvider.GetServices<IHostedService>()
                    .OfType<AutoUpdateService>()
                    .FirstOrDefault();

                if (autoUpdateService != null)
                {
                    _logger.LogInformation("Triggering auto-update process...");
                    await autoUpdateService.CheckAndApplyUpdateAsync();
                }
                else
                {
                    _logger.LogWarning("AutoUpdateService not found. Cannot perform update.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering software update from MQTT command");
            }
        }
        // Check if this is a REMOTEPORT command
        else if (topic.EndsWith("/SIPCMD/REMOTEPORT", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Received REMOTEPORT command via MQTT. Payload: {Payload}", payload);

            try
            {
                if (int.TryParse(payload.Trim(), out var port))
                {
                    var autoSshService = _serviceProvider.GetRequiredService<AutoSshService>();
                    await autoSshService.SetRemotePortAsync(port);
                    _logger.LogInformation("Remote port set to {Port} for autossh", port);
                }
                else
                {
                    _logger.LogWarning("Invalid port number received: {Payload}", payload);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing REMOTEPORT command");
            }
        }
        // Check if this is an EXTENSION command
        else if (topic.EndsWith("/SIPCMD/EXTENSION", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Received EXTENSION command via MQTT. Processing extension data...");

            try
            {
                // Parse the JSON payload
                var extensionData = JsonSerializer.Deserialize<PjsipExtension>(payload, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (extensionData == null || string.IsNullOrWhiteSpace(extensionData.Name))
                {
                    _logger.LogWarning("Invalid extension data received. Extension name is required.");
                    return;
                }

                // Get the PjsipManagementService from the service provider
                using var scope = _serviceProvider.CreateScope();
                var pjsipService = scope.ServiceProvider.GetRequiredService<PjsipManagementService>();
                var extConfService = scope.ServiceProvider.GetRequiredService<ExtensionsConfManagementService>();

                // Parse current configuration
                var config = await pjsipService.ParseAsync();

                // Check if extension exists
                var existingExtension = pjsipService.FindExtension(config, extensionData.Name);

                if (existingExtension != null)
                {
                    _logger.LogInformation("Modifying existing extension: {ExtensionName}", extensionData.Name);
                    pjsipService.RemoveExtension(config, extensionData.Name);
                }
                else
                {
                    _logger.LogInformation("Adding new extension: {ExtensionName}", extensionData.Name);
                }

                // Add the extension
                pjsipService.AddExtension(config, extensionData);

                // Save configuration
                await pjsipService.SaveAsync(config);

                // Add to extensions.conf
                await extConfService.AddOrUpdateExtensionAsync(extensionData.Name);

                // Reload PJSIP
                var reloadResult = await pjsipService.ReloadAsync();
                _logger.LogInformation("Extension {ExtensionName} processed successfully in pjsip.conf and extensions.conf. Reload result: {Result}", 
                    extensionData.Name, reloadResult);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error parsing extension JSON payload: {Payload}", payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing extension command");
            }
        }
        else
        {
            _logger.LogInformation("Received command on topic {Topic} with payload: {Payload}", topic, payload);
        }
    }

    private async Task PublishHeartbeatAsync(CancellationToken cancellationToken)
    {
        if (_mqttClient?.IsConnected != true)
        {
            _logger.LogWarning("Cannot send heartbeat - MQTT client is not connected");
            return;
        }

        try
        {
            var topic = $"tele/UBI/{_macAddress}/HEARTBEAT";
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
            var timestamp = DateTime.UtcNow.ToString("o");
            var localIp = GetLocalIpAddress();
            var payload = $"{{\"timestamp\":\"{timestamp}\",\"version\":\"{version}\",\"localIp\":\"{localIp}\"}}";

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .Build();

            await _mqttClient.PublishAsync(message, cancellationToken);

            _logger.LogDebug("Heartbeat sent to {Topic} with version {Version} and IP {LocalIp}", topic, version, localIp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish heartbeat");
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

    private static string GetLocalIpAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    return ip.ToString();
                }
            }
        }
        catch (Exception)
        {
            // Fallback method using NetworkInterface
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up
                             && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .ToList();

                foreach (var nic in nics)
                {
                    var ipProps = nic.GetIPProperties();
                    var ipAddress = ipProps.UnicastAddresses
                        .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork
                                          && !IPAddress.IsLoopback(a.Address));

                    if (ipAddress != null)
                    {
                        return ipAddress.Address.ToString();
                    }
                }
            }
            catch
            {
                // Ignore fallback errors
            }
        }

        return "Unknown";
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping MQTT service");

        if (_mqttClient?.IsConnected == true)
        {
            await _mqttClient.DisconnectAsync(cancellationToken: cancellationToken);
        }

        _mqttClient?.Dispose();

        await base.StopAsync(cancellationToken);
    }

    public string? GetMacAddressValue()
    {
        return _macAddress;
    }

    public async Task PublishMessageAsync(string topic, string payload, CancellationToken cancellationToken = default)
    {
        if (_mqttClient?.IsConnected != true)
        {
            _logger.LogWarning("Cannot publish message - MQTT client is not connected");
            return;
        }

        try
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .Build();

            await _mqttClient.PublishAsync(message, cancellationToken);
            _logger.LogInformation("Published message to topic: {Topic}", topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to topic: {Topic}", topic);
        }
    }
}
