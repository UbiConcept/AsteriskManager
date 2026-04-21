namespace AsteriskManager.Services;
public class AsteriskSettings
{
    public string ExtensionsPath { get; set; } = "/etc/asterisk/extensions.conf";
    public string PjsipPath { get; set; } = "/etc/asterisk/pjsip.conf";
    public string LogPath { get; set; } = "/var/log/asterisk/full";
    public string MessageLogPath { get; set; } = "/var/log/asterisk/messages.log";
}
