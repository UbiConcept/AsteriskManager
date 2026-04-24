using Microsoft.Extensions.Options;
using System.Text;
using System.Text.RegularExpressions;

namespace AsteriskManager.Services;

public class ExtensionsConfManagementService
{
    private readonly AsteriskSettings _settings;
    private readonly ILogger<ExtensionsConfManagementService> _logger;

    public ExtensionsConfManagementService(
        IOptions<AsteriskSettings> options,
        ILogger<ExtensionsConfManagementService> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Adds or updates an extension in the [from-internal] context
    /// </summary>
    public async Task AddOrUpdateExtensionAsync(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            _logger.LogWarning("Cannot add empty extension to extensions.conf");
            return;
        }

        try
        {
            var content = await File.ReadAllTextAsync(_settings.ExtensionsPath);
            var lines = content.Split('\n').ToList();

            // Find [from-internal] section
            var fromInternalIndex = FindSectionIndex(lines, "from-internal");

            //if (fromInternalIndex == -1)
            //{
                // Section doesn't exist, create it
                _logger.LogInformation("Creating [from-internal] section in extensions.conf");
                lines.Add("");
                lines.Add("[from-internal]");
                fromInternalIndex = lines.Count - 1;
            //}

            // Check if extension already exists in this section
            var existingIndex = FindExtensionInSection(lines, fromInternalIndex, extension);

            if (existingIndex != -1)
            {
                _logger.LogInformation("Extension {Extension} already exists in [from-internal], skipping", extension);
                return;
            }

            // Find the end of the section (next section or end of file)
            var insertIndex = FindSectionEnd(lines, fromInternalIndex);

            // Add the extension
            var dialLine = $"exten => {extension},1,Dial(PJSIP/{extension},20)";
            var hangupLine = $" same => n,Hangup()";

            lines.Insert(insertIndex, dialLine);
            lines.Insert(insertIndex + 1, hangupLine);

            // Write back to file
            await File.WriteAllTextAsync(_settings.ExtensionsPath, string.Join("\n", lines));

            _logger.LogInformation("Added extension {Extension} to [from-internal] in extensions.conf", extension);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add extension {Extension} to extensions.conf", extension);
            throw;
        }
    }

    /// <summary>
    /// Removes an extension from the [from-internal] context
    /// </summary>
    public async Task RemoveExtensionAsync(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            _logger.LogWarning("Cannot remove empty extension from extensions.conf");
            return;
        }

        try
        {
            var content = await File.ReadAllTextAsync(_settings.ExtensionsPath);
            var lines = content.Split('\n').ToList();

            // Find [from-internal] section
            var fromInternalIndex = FindSectionIndex(lines, "from-internal");

            if (fromInternalIndex == -1)
            {
                _logger.LogWarning("Section [from-internal] not found in extensions.conf");
                return;
            }

            // Find the extension
            var extensionIndex = FindExtensionInSection(lines, fromInternalIndex, extension);

            if (extensionIndex == -1)
            {
                _logger.LogInformation("Extension {Extension} not found in [from-internal]", extension);
                return;
            }

            // Remove the extension line and its continuation lines (same => n,...)
            var linesToRemove = new List<int> { extensionIndex };

            // Find continuation lines (same => n,...)
            for (int i = extensionIndex + 1; i < lines.Count; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.StartsWith("same =>") || trimmed.StartsWith("same=>"))
                {
                    linesToRemove.Add(i);
                }
                else if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    break;
                }
            }

            // Remove in reverse order to maintain indices
            for (int i = linesToRemove.Count - 1; i >= 0; i--)
            {
                lines.RemoveAt(linesToRemove[i]);
            }

            // Write back to file
            await File.WriteAllTextAsync(_settings.ExtensionsPath, string.Join("\n", lines));

            _logger.LogInformation("Removed extension {Extension} from [from-internal] in extensions.conf", extension);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove extension {Extension} from extensions.conf", extension);
            throw;
        }
    }

    /// <summary>
    /// Checks if an extension exists in the [from-internal] context
    /// </summary>
    public async Task<bool> ExtensionExistsAsync(string extension)
    {
        try
        {
            var content = await File.ReadAllTextAsync(_settings.ExtensionsPath);
            var lines = content.Split('\n').ToList();

            var fromInternalIndex = FindSectionIndex(lines, "from-internal");
            if (fromInternalIndex == -1)
                return false;

            return FindExtensionInSection(lines, fromInternalIndex, extension) != -1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if extension {Extension} exists", extension);
            return false;
        }
    }

    private int FindSectionIndex(List<string> lines, string sectionName)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed == $"[{sectionName}]")
                return i;
        }
        return -1;
    }

    private int FindSectionEnd(List<string> lines, int sectionStartIndex)
    {
        for (int i = sectionStartIndex + 1; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            // New section starts
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                return i;
        }
        return lines.Count;
    }

    private int FindExtensionInSection(List<string> lines, int sectionStartIndex, string extension)
    {
        var sectionEnd = FindSectionEnd(lines, sectionStartIndex);

        // Pattern to match: exten => {extension},1,...
        var pattern = $@"^\s*exten\s*=>\s*{Regex.Escape(extension)}\s*,\s*1\s*,";
        var regex = new Regex(pattern);

        for (int i = sectionStartIndex + 1; i < sectionEnd; i++)
        {
            if (regex.IsMatch(lines[i]))
                return i;
        }
        return -1;
    }
}
