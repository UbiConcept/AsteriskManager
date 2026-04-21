namespace AsteriskManager.Services;

public class AutoUpdateSettings
{
    public string UpdateUrl { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int CheckIntervalMinutes { get; set; } = 60;
}
