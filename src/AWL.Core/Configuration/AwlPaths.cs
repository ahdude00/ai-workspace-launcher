namespace AWL.Core.Configuration;

/// <summary>Resolves AWL and Claude Code paths from the current user's directories.</summary>
public sealed class AwlPaths
{
    private const string AwlDirectoryName = "AWL";
    private const string ClaudeConfigDirectoryVariable = "CLAUDE_CONFIG_DIR";

    /// <summary>Builds paths from the supplied user directories and optional Claude directory.</summary>
    public AwlPaths(
        string localApplicationData,
        string roamingApplicationData,
        string userProfile,
        string? claudeConfigDirectory = null)
    {
        string localData = Path.GetFullPath(localApplicationData);
        string profile = Path.GetFullPath(userProfile);

        AwlHome = Path.Combine(localData, AwlDirectoryName);
        RoamingDataDirectory = Path.Combine(Path.GetFullPath(roamingApplicationData), AwlDirectoryName);
        DefaultInstallDirectory = Path.Combine(localData, "Programs", AwlDirectoryName);
        DefaultClaudeExecutable = Path.Combine(profile, ".local", "bin", "claude.exe");
        ClaudeConfigDirectory = string.IsNullOrWhiteSpace(claudeConfigDirectory)
            ? Path.Combine(profile, ".claude")
            : Path.GetFullPath(claudeConfigDirectory);
    }

    /// <summary>Gets the local AWL data directory used as AWL_HOME.</summary>
    public string AwlHome { get; }

    /// <summary>Gets the AWL data directory under roaming application data.</summary>
    public string RoamingDataDirectory { get; }

    /// <summary>Gets the default user installation directory for AWL.</summary>
    public string DefaultInstallDirectory { get; }

    /// <summary>Gets the directory for AWL's Claude configuration files.</summary>
    public string ConfigDirectory => Path.Combine(AwlHome, "config");

    /// <summary>Gets the directory for AWL log files.</summary>
    public string LogsDirectory => Path.Combine(AwlHome, "logs");

    /// <summary>Gets the directory for tools managed by AWL.</summary>
    public string ToolsDirectory => Path.Combine(AwlHome, "tools");

    /// <summary>Gets the directory for downloaded and generated caches.</summary>
    public string CacheDirectory => Path.Combine(AwlHome, "cache");

    /// <summary>Gets the local directory for Python runtimes managed by uv.</summary>
    public string PythonInstallDirectory => Path.Combine(AwlHome, "runtime", "python");

    /// <summary>Gets the cache directory used by AWL's uv commands.</summary>
    public string UvCacheDirectory => Path.Combine(CacheDirectory, "uv");

    /// <summary>Gets the directory for AWL's Python virtual environment.</summary>
    public string VirtualEnvironmentDirectory => Path.Combine(AwlHome, ".venv");

    /// <summary>Gets the path to the virtual environment's Python executable.</summary>
    public string PythonExecutable => Path.Combine(VirtualEnvironmentDirectory, "Scripts", "python.exe");

    /// <summary>Gets the path to the UDF tool managed by AWL.</summary>
    public string UdfToolFile => Path.Combine(ToolsDirectory, "udf_tool.py");

    /// <summary>Gets the path to AWL's Claude settings file.</summary>
    public string ClaudeSettingsFile => Path.Combine(ConfigDirectory, "claude-settings.json");

    /// <summary>Gets the path to the context file passed to Claude.</summary>
    public string ContextFile => Path.Combine(ConfigDirectory, "awl-context.md");

    /// <summary>Gets the path to AWL's own settings file.</summary>
    public string SettingsFile => Path.Combine(RoamingDataDirectory, "settings.json");

    /// <summary>Gets the path to AWL's local database.</summary>
    public string DatabaseFile => Path.Combine(RoamingDataDirectory, "awl.db");

    /// <summary>Gets the executable path used by Claude Code's default user installation.</summary>
    public string DefaultClaudeExecutable { get; }

    /// <summary>Gets the effective Claude Code data directory.</summary>
    public string ClaudeConfigDirectory { get; }

    /// <summary>Gets the root directory for Claude Code conversation transcripts.</summary>
    public string ClaudeProjectsDirectory => Path.Combine(ClaudeConfigDirectory, "projects");

    /// <summary>Gets the root directory for Claude Code session debug logs.</summary>
    public string ClaudeDebugDirectory => Path.Combine(ClaudeConfigDirectory, "debug");

    /// <summary>Uses the platform's known folders and CLAUDE_CONFIG_DIR from the current process.</summary>
    public static AwlPaths FromEnvironment() => new(
        GetKnownFolder(Environment.SpecialFolder.LocalApplicationData),
        GetKnownFolder(Environment.SpecialFolder.ApplicationData),
        GetKnownFolder(Environment.SpecialFolder.UserProfile),
        Environment.GetEnvironmentVariable(ClaudeConfigDirectoryVariable));

    private static string GetKnownFolder(Environment.SpecialFolder folder)
    {
        string path = Environment.GetFolderPath(folder, Environment.SpecialFolderOption.DoNotVerify);
        return string.IsNullOrWhiteSpace(path)
            ? throw new InvalidOperationException($"Known folder {folder} is unavailable.")
            : path;
    }
}
