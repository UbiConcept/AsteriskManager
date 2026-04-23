using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AsteriskManager.Services;

public class LogLevelService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LogLevelService> _logger;
    private readonly string _appsettingsPath;

    public LogLevelService(IConfiguration configuration, ILogger<LogLevelService> logger, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _logger = logger;
        _appsettingsPath = Path.Combine(environment.ContentRootPath, "appsettings.json");
    }

    public string GetCurrentLogLevel()
    {
        return _configuration["Logging:LogLevel:AsteriskManager"] ?? "Information";
    }

    public async Task<bool> SetLogLevelAsync(string logLevel)
    {
        try
        {
            if (!File.Exists(_appsettingsPath))
            {
                _logger.LogError("appsettings.json not found at {Path}", _appsettingsPath);
                return false;
            }

            var json = await File.ReadAllTextAsync(_appsettingsPath);
            var jsonDocument = JsonDocument.Parse(json);
            var root = jsonDocument.RootElement;

            // Create a mutable dictionary from the JSON
            var settings = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            
            if (settings == null)
            {
                _logger.LogError("Failed to deserialize appsettings.json");
                return false;
            }

            // Navigate to Logging.LogLevel.AsteriskManager
            if (!settings.ContainsKey("Logging"))
            {
                settings["Logging"] = new Dictionary<string, object>();
            }

            var logging = settings["Logging"] as JsonElement? ?? JsonSerializer.SerializeToElement(settings["Logging"]);
            var loggingDict = JsonSerializer.Deserialize<Dictionary<string, object>>(logging.GetRawText()) ?? new Dictionary<string, object>();

            if (!loggingDict.ContainsKey("LogLevel"))
            {
                loggingDict["LogLevel"] = new Dictionary<string, object>();
            }

            var logLevelSection = loggingDict["LogLevel"] as JsonElement? ?? JsonSerializer.SerializeToElement(loggingDict["LogLevel"]);
            var logLevelDict = JsonSerializer.Deserialize<Dictionary<string, object>>(logLevelSection.GetRawText()) ?? new Dictionary<string, object>();

            logLevelDict["AsteriskManager"] = logLevel;
            loggingDict["LogLevel"] = logLevelDict;
            settings["Logging"] = loggingDict;

            // Write back to file with formatting
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var updatedJson = JsonSerializer.Serialize(settings, options);
            await File.WriteAllTextAsync(_appsettingsPath, updatedJson);

            _logger.LogInformation("Log level changed to {LogLevel}", logLevel);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating log level");
            return false;
        }
    }
}
