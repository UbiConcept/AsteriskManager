using Microsoft.Extensions.Options;

namespace AsteriskManager.Services;

public class PjsipManagementService
{
    private readonly AsteriskSettings _settings;
    private readonly AsteriskService _asterisk;

    public PjsipManagementService(IOptions<AsteriskSettings> options, AsteriskService asterisk)
    {
        _settings = options.Value;
        _asterisk = asterisk;
    }

    public async Task<PjsipParseResult> ParseAsync()
    {
        var text = await File.ReadAllTextAsync(_settings.PjsipPath);
        return Parse(text);
    }

    public PjsipParseResult Parse(string text)
    {
        var result = new PjsipParseResult();
        var lines = text.Split('\n');
        string? currentSection = null;
        string? currentType = null;
        var currentProps = new Dictionary<string, string>();
        var headerLines = new List<string>();
        var footerStarted = false;
        var footerLines = new List<string>();
        var transportLines = new List<string>();
        var sections = new List<(string name, string type, Dictionary<string, string> props)>();
        bool inHeader = true;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.StartsWith('[') && line.Contains(']'))
            {
                inHeader = false;
                if (currentSection != null)
                    sections.Add((currentSection, currentType ?? "", new Dictionary<string, string>(currentProps)));
                currentSection = line.Substring(1, line.IndexOf(']') - 1);
                currentType = null;
                currentProps = new Dictionary<string, string>();
                continue;
            }
            if (inHeader && currentSection == null)
            {
                headerLines.Add(line);
                continue;
            }
            var trimmed = line.Trim();
            if (trimmed.StartsWith(';') || string.IsNullOrEmpty(trimmed))
            {
                if (currentSection != null && currentType == null && sections.Count > 0)
                {
                    // could be footer
                }
                continue;
            }
            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx > 0)
            {
                var key = trimmed.Substring(0, eqIdx).Trim();
                var val = trimmed.Substring(eqIdx + 1).Trim();
                if (key == "type") currentType = val;
                else currentProps[key] = val;
            }
        }
        if (currentSection != null)
            sections.Add((currentSection, currentType ?? "", new Dictionary<string, string>(currentProps)));

        // Group into extensions
        var extMap = new Dictionary<string, PjsipExtension>();
        foreach (var (name, type, props) in sections)
        {
            if (type == "transport")
            {
                transportLines.Add("[" + name + "]");
                transportLines.Add("type=transport");
                foreach (var kv in props)
                    transportLines.Add(kv.Key + "=" + kv.Value);
                transportLines.Add("");
                continue;
            }
            if (type != "endpoint" && type != "auth" && type != "aor")
            {
                // Unknown section, preserve in transport area
                transportLines.Add("[" + name + "]");
                transportLines.Add("type=" + type);
                foreach (var kv in props)
                    transportLines.Add(kv.Key + "=" + kv.Value);
                transportLines.Add("");
                continue;
            }
            if (!extMap.ContainsKey(name))
                extMap[name] = new PjsipExtension { Name = name };
            var ext = extMap[name];
            if (type == "endpoint")
            {
                ext.Context = props.GetValueOrDefault("context", "from-internal");
                ext.Disallow = props.GetValueOrDefault("disallow", "all");
                ext.Allow = props.GetValueOrDefault("allow", "ulaw");
                ext.RtpSymmetric = props.GetValueOrDefault("rtp_symmetric", "yes") == "yes";
                ext.RewriteContact = props.GetValueOrDefault("rewrite_contact", "yes") == "yes";
                ext.ForceRport = props.GetValueOrDefault("force_rport", "yes") == "yes";
                foreach (var kv in props)
                {
                    if (kv.Key is "context" or "disallow" or "allow" or "auth" or "aors" or "rtp_symmetric" or "rewrite_contact" or "force_rport") continue;
                    ext.EndpointExtra[kv.Key] = kv.Value;
                }
            }
            else if (type == "auth")
            {
                ext.AuthType = props.GetValueOrDefault("auth_type", "userpass");
                ext.Username = props.GetValueOrDefault("username", name);
                ext.Password = props.GetValueOrDefault("password", "");
                foreach (var kv in props)
                {
                    if (kv.Key is "auth_type" or "username" or "password") continue;
                    ext.AuthExtra[kv.Key] = kv.Value;
                }
            }
            else if (type == "aor")
            {
                ext.MaxContacts = int.TryParse(props.GetValueOrDefault("max_contacts", "1"), out var mc) ? mc : 1;
                foreach (var kv in props)
                {
                    if (kv.Key == "max_contacts") continue;
                    ext.AorExtra[kv.Key] = kv.Value;
                }
            }
        }

        result.Header = string.Join('\n', headerLines);
        result.TransportConfig = string.Join('\n', transportLines);
        result.Extensions = extMap.Values.ToList();
        return result;
    }

    public string Serialize(PjsipParseResult data)
    {
        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(data.Header))
            sb.AppendLine(data.Header.TrimEnd());
        sb.AppendLine();
        if (!string.IsNullOrEmpty(data.TransportConfig))
        {
            sb.AppendLine(data.TransportConfig.TrimEnd());
            sb.AppendLine();
        }
        foreach (var ext in data.Extensions)
        {
            sb.AppendLine("[" + ext.Name + "]");
            sb.AppendLine("type=endpoint");
            sb.AppendLine("context=" + ext.Context);
            sb.AppendLine("disallow=" + ext.Disallow);
            sb.AppendLine("allow=" + ext.Allow);
            sb.AppendLine("auth=" + ext.Name);
            sb.AppendLine("aors=" + ext.Name);
            sb.AppendLine("rtp_symmetric=" + (ext.RtpSymmetric ? "yes" : "no"));
            sb.AppendLine("rewrite_contact=" + (ext.RewriteContact ? "yes" : "no"));
            sb.AppendLine("force_rport=" + (ext.ForceRport ? "yes" : "no"));
            foreach (var kv in ext.EndpointExtra)
                sb.AppendLine(kv.Key + "=" + kv.Value);
            sb.AppendLine();
            sb.AppendLine("[" + ext.Name + "]");
            sb.AppendLine("type=auth");
            sb.AppendLine("auth_type=" + ext.AuthType);
            sb.AppendLine("username=" + ext.Username);
            sb.AppendLine("password=" + ext.Password);
            foreach (var kv in ext.AuthExtra)
                sb.AppendLine(kv.Key + "=" + kv.Value);
            sb.AppendLine();
            sb.AppendLine("[" + ext.Name + "]");
            sb.AppendLine("type=aor");
            sb.AppendLine("max_contacts=" + ext.MaxContacts);
            foreach (var kv in ext.AorExtra)
                sb.AppendLine(kv.Key + "=" + kv.Value);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public async Task SaveAsync(PjsipParseResult data)
    {
        var content = Serialize(data);
        await File.WriteAllTextAsync(_settings.PjsipPath, content);
    }

    public async Task<string> ReloadAsync()
    {
        return await _asterisk.ReloadPjsipAsync();
    }

    public PjsipExtension? FindExtension(PjsipParseResult data, string name)
    {
        return data.Extensions.FirstOrDefault(e => e.Name == name);
    }

    public void AddExtension(PjsipParseResult data, PjsipExtension ext)
    {
        data.Extensions.Add(ext);
    }

    public void RemoveExtension(PjsipParseResult data, string name)
    {
        data.Extensions.RemoveAll(e => e.Name == name);
    }
}

public class PjsipParseResult
{
    public string Header { get; set; } = string.Empty;
    public string TransportConfig { get; set; } = string.Empty;
    public List<PjsipExtension> Extensions { get; set; } = new();
}
