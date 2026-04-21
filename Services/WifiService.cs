using System.Diagnostics;

namespace AsteriskManager.Services;

public class WifiNetwork
{
    public string Ssid { get; set; } = string.Empty;
    public string Signal { get; set; } = string.Empty;
    public string Security { get; set; } = string.Empty;
    public bool InUse { get; set; }
}

public class WifiService
{
    public async Task<List<WifiNetwork>> ScanAsync()
    {
        var output = await RunAsync("nmcli", "-t -f IN-USE,SSID,SIGNAL,SECURITY device wifi list --rescan yes");
        var networks = new List<WifiNetwork>();
        var seen = new HashSet<string>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(':', 4);
            if (parts.Length < 4) continue;
            var ssid = parts[1].Trim();
            if (string.IsNullOrEmpty(ssid) || !seen.Add(ssid)) continue;
            networks.Add(new WifiNetwork
            {
                InUse = parts[0].Trim() == "*",
                Ssid = ssid,
                Signal = parts[2].Trim(),
                Security = parts[3].Trim()
            });
        }
        return networks.OrderByDescending(n => n.InUse).ThenByDescending(n => int.TryParse(n.Signal, out var s) ? s : 0).ToList();
    }

    public async Task<string> ConnectAsync(string ssid, string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return await RunAsync("nmcli", "device wifi connect \"" + ssid + "\"");
        return await RunAsync("nmcli", "device wifi connect \"" + ssid + "\" password \"" + password + "\"");
    }

    public async Task<string> DisconnectAsync()
    {
        return await RunAsync("nmcli", "device disconnect wlan0");
    }

    public async Task<string> GetStatusAsync()
    {
        return await RunAsync("nmcli", "device show wlan0");
    }

    public async Task<List<string>> GetSavedConnectionsAsync()
    {
        var output = await RunAsync("nmcli", "-t -f NAME,TYPE connection show");
        var connections = new List<string>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(':', 2);
            if (parts.Length == 2 && parts[1].Contains("wireless"))
                connections.Add(parts[0]);
        }
        return connections;
    }

    public async Task<string> ForgetConnectionAsync(string name)
    {
        return await RunAsync("nmcli", "connection delete \"" + name + "\"");
    }

    private async Task<string> RunAsync(string fileName, string arguments)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return process.ExitCode == 0 ? output : "Error: " + error;
        }
        catch (Exception ex)
        {
            return "Failed: " + ex.Message;
        }
    }
}
