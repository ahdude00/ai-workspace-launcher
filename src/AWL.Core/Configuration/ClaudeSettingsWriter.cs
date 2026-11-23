using System.Text.Json;

namespace AWL.Core.Configuration;

/// <summary>Writes the settings passed to Claude Code for every AWL session.</summary>
public static class ClaudeSettingsWriter
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static void Write(Stream destination, AwlPaths paths, AwlSettings settings)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(settings);
        JsonSerializer.Serialize(destination, new
        {
            permissions = new { defaultMode = "default" },
            cleanupPeriodDays = settings.RetentionDays,
            env = new { AWL_HOME = paths.AwlHome, VIRTUAL_ENV = paths.VirtualEnvironmentDirectory },
        }, Options);
    }
}
