namespace AsteriskManager.Services;

public class PjsipExtension
{
    public string Name { get; set; } = string.Empty;
    // Endpoint
    public string Context { get; set; } = "from-internal";
    public string Disallow { get; set; } = "all";
    public string Allow { get; set; } = "ulaw";
    public bool RtpSymmetric { get; set; } = true;
    public bool RewriteContact { get; set; } = true;
    public bool ForceRport { get; set; } = true;
    public Dictionary<string, string> EndpointExtra { get; set; } = new();
    // Auth
    public string AuthType { get; set; } = "userpass";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public Dictionary<string, string> AuthExtra { get; set; } = new();
    // AOR
    public int MaxContacts { get; set; } = 1;
    public Dictionary<string, string> AorExtra { get; set; } = new();
}
