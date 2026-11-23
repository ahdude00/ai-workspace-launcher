using System.Text.Json;
using System.Text.Json.Serialization;
using AWL.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace AWL.Infrastructure.Configuration;

internal sealed partial class JsonAwlSettingsStore(AwlPaths paths, ILogger<JsonAwlSettingsStore> logger)
    : IAwlSettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter<TerminalPreference>(allowIntegerValues: false),
            new JsonStringEnumConverter<SetupStepStatus>(allowIntegerValues: false),
        },
    };

    public AwlSettings Load()
    {
        try
        {
            using var file = File.Open(paths.SettingsFile, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete);
            var settings = JsonSerializer.Deserialize<AwlSettings>(file, Options);
            if (settings is not null && IsValid(settings))
            {
                return settings;
            }
        }
        catch (FileNotFoundException)
        {
            return new AwlSettings();
        }
        catch (DirectoryNotFoundException)
        {
            return new AwlSettings();
        }
        catch (JsonException)
        {
            // Log only the event. Exception messages may contain settings data.
        }

        LogInvalidSettings(logger);
        return new AwlSettings();
    }

    public void Save(AwlSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!IsValid(settings))
        {
            throw new ArgumentException("Invalid AWL settings.", nameof(settings));
        }

        Directory.CreateDirectory(paths.RoamingDataDirectory);
        string temporaryFile = Path.Combine(paths.RoamingDataDirectory, Path.GetRandomFileName());
        var file = new FileStream(temporaryFile, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        try
        {
            using (file)
            {
                JsonSerializer.Serialize(file, settings, Options);
                file.Flush(flushToDisk: true);
            }

            File.Move(temporaryFile, paths.SettingsFile, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryFile);
        }
    }

    private static bool IsValid(AwlSettings settings) =>
        settings.RetentionDays > 0 && Enum.IsDefined(settings.TerminalPreference)
        && settings.SetupSteps is not null
        && settings.SetupSteps.All(step => !string.IsNullOrWhiteSpace(step.Key) && Enum.IsDefined(step.Value));

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Invalid AWL settings. Using defaults.")]
    private static partial void LogInvalidSettings(ILogger logger);
}
