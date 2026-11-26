namespace AWL.Core.Terminal;

/// <summary>Inputs for a terminal launcher. Each argument is a separate, unquoted value.</summary>
public sealed record ClaudeLaunchSpec(string Executable, IReadOnlyList<string> Arguments, string WorkingDirectory)
{
    public Guid? WorkspaceSessionId { get; init; }
    public bool OpenExternally { get; init; }
}
