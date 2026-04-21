using System.Net.NetworkInformation;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace AsteriskManager.Services;

public class MqttService : BackgroundService
{
    private readonly ILogger<MqttService> _logger;
    private readonly MqttSettings _settings;
    private IMqttClient? _mqttClient;
    private string? _macAddress;

    public MqttService(ILogger<MqttService> logger, IOptions<MqttSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
        await Task.CompletedTask;
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
            var payload = DateTime.UtcNow.ToString("o");

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .Build();

            await _mqttClient.PublishAsync(message, cancellationToken);

            _logger.LogDebug("Heartbeat sent to {Topic}", topic);
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

        return string.Join(":", Enumerable.Range(0, mac.Length / 2)
            .Select(i => mac.Substring(i * 2, 2)));
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
}
