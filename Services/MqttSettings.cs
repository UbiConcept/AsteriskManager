namespace AsteriskManager.Services;

public class MqttSettings
{
    public string Server { get; set; } = string.Empty;
    public int Port { get; set; } = 1883;
    public string Password { get; set; } = string.Empty;
}
