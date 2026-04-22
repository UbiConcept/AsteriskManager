using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace AsteriskManager.Services;

public class AsteriskService
{
    private readonly AsteriskSettings _settings;

    public AsteriskService(IOptions<AsteriskSettings> options)
    {
        _settings = options.Value;
    }

    public async Task<string> ReadExtensionsAsync()
    {
        if (!File.Exists(_settings.ExtensionsPath))
            return "; extensions.conf not found at " + _settings.ExtensionsPath;
        return await File.ReadAllTextAsync(_settings.ExtensionsPath);
    }

    public async Task SaveExtensionsAsync(string content)
    {
        var dir = Path.GetDirectoryName(_settings.ExtensionsPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(_settings.ExtensionsPath, content);
    }

    public async Task<string> ReadPjsipAsync()
    {
        if (!File.Exists(_settings.PjsipPath))
            return "; pjsip.conf not found at " + _settings.PjsipPath;
        return await File.ReadAllTextAsync(_settings.PjsipPath);
    }

    public async Task SavePjsipAsync(string content)
    {
        var dir = Path.GetDirectoryName(_settings.PjsipPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(_settings.PjsipPath, content);
    }

    public async Task<string> ReloadDialplanAsync()
    {
        return await RunAsteriskCommandAsync("dialplan reload");
    }

    public async Task<string> ReloadPjsipAsync()
    {
        return await RunAsteriskCommandAsync("pjsip reload");
    }

    public async Task<string> RestartAsteriskAsync()
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = "restart asterisk",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                return "Asterisk service restart initiated successfully.";
            }
            else
            {
                return "Error restarting Asterisk service.\n" + error;
            }
        }
        catch (Exception ex)
        {
            return "Failed to restart Asterisk service: " + ex.Message;
        }
    }

    private async Task<string> RunAsteriskCommandAsync(string command)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "asterisk",
                Arguments = "-rx \"" + command + "\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return process.ExitCode == 0 ? command + " completed.\n" + output : "Error running " + command + ".\n" + error;
        }
        catch (Exception ex)
        {
            return "Failed to execute asterisk CLI: " + ex.Message;
        }
    }

    public async Task<string> ReadLogTailAsync(int lines = 200)
    {
        return await TailFileAsync(_settings.LogPath, lines);
    }

    public async Task<string> ReadMessageLogTailAsync(int lines = 200)
    {
        return await TailFileAsync(_settings.MessageLogPath, lines);
    }

    private async Task<string> TailFileAsync(string path, int lines)
    {
        if (!File.Exists(path))
            return "Log file not found at " + path;
        var allLines = await File.ReadAllLinesAsync(path);
        var tail = allLines.Skip(Math.Max(0, allLines.Length - lines));
        return string.Join("\n", tail);
    }
}