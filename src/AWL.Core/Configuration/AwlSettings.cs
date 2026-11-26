namespace AWL.Core.Configuration;

public enum TerminalPreference
{
    WindowsTerminal,
    WindowsConsole,
    Embedded,
}

public enum SetupStepStatus
{
    Pending,
    Completed,
    Failed,
}

/// <summary>AWL preferences and the last known state of each setup step.</summary>
public sealed record AwlSettings
{
    public int RetentionDays { get; init; } = 3650;

    public TerminalPreference TerminalPreference { get; init; } = TerminalPreference.WindowsTerminal;

    public IReadOnlyDictionary<string, SetupStepStatus> SetupSteps { get; init; }
        = new Dictionary<string, SetupStepStatus>();
}
