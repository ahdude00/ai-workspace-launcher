using AWL.Core.Configuration;

namespace AWL.Core.Tests.Configuration;

public sealed class AwlPathsTests
{
    private static readonly string _localData = Path.Combine(Path.GetTempPath(), "Yerel Veriler");
    private static readonly string _roamingData = Path.Combine(Path.GetTempPath(), "Dolaşan Veriler");
    private static readonly string _profile = Path.Combine(Path.GetTempPath(), "Örnek Profil");

    [Fact]
    public void Resolves_awl_files_from_their_documented_directories()
    {
        var paths = new AwlPaths(_localData, _roamingData, _profile);
        string awlHome = Path.Combine(_localData, "AWL");
        string roamingHome = Path.Combine(_roamingData, "AWL");

        paths.AwlHome.ShouldBe(awlHome);
        paths.RoamingDataDirectory.ShouldBe(roamingHome);
        paths.DefaultInstallDirectory.ShouldBe(Path.Combine(_localData, "Programs", "AWL"));
        paths.ConfigDirectory.ShouldBe(Path.Combine(awlHome, "config"));
        paths.LogsDirectory.ShouldBe(Path.Combine(awlHome, "logs"));
        paths.CacheDirectory.ShouldBe(Path.Combine(awlHome, "cache"));
        paths.ToolsDirectory.ShouldBe(Path.Combine(awlHome, "tools"));
        paths.ClaudeSettingsFile.ShouldBe(Path.Combine(awlHome, "config", "claude-settings.json"));
        paths.ContextFile.ShouldBe(Path.Combine(awlHome, "config", "awl-context.md"));
        paths.SettingsFile.ShouldBe(Path.Combine(roamingHome, "settings.json"));
        paths.DatabaseFile.ShouldBe(Path.Combine(roamingHome, "awl.db"));
    }

    [Fact]
    public void Resolves_python_and_tool_paths_under_awl_home()
    {
        var paths = new AwlPaths(_localData, _roamingData, _profile);
        string awlHome = Path.Combine(_localData, "AWL");

        paths.PythonInstallDirectory.ShouldBe(Path.Combine(awlHome, "runtime", "python"));
        paths.UvCacheDirectory.ShouldBe(Path.Combine(awlHome, "cache", "uv"));
        paths.VirtualEnvironmentDirectory.ShouldBe(Path.Combine(awlHome, ".venv"));
        paths.PythonExecutable.ShouldBe(Path.Combine(awlHome, ".venv", "Scripts", "python.exe"));
        paths.UdfToolFile.ShouldBe(Path.Combine(awlHome, "tools", "udf_tool.py"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Uses_the_profile_for_claude_without_a_config_directory(string? configDirectory)
    {
        var paths = new AwlPaths(_localData, _roamingData, _profile, configDirectory);
        string claudeHome = Path.Combine(_profile, ".claude");

        paths.DefaultClaudeExecutable.ShouldBe(Path.Combine(_profile, ".local", "bin", "claude.exe"));
        paths.ClaudeConfigDirectory.ShouldBe(claudeHome);
        paths.ClaudeProjectsDirectory.ShouldBe(Path.Combine(claudeHome, "projects"));
        paths.ClaudeDebugDirectory.ShouldBe(Path.Combine(claudeHome, "debug"));
    }

    [Fact]
    public void Uses_the_config_override_for_claude_history_paths()
    {
        string configDirectory = Path.Combine(Path.GetTempPath(), "Özel Claude Ayarları");
        var paths = new AwlPaths(_localData, _roamingData, _profile, configDirectory);

        paths.ClaudeConfigDirectory.ShouldBe(configDirectory);
        paths.ClaudeProjectsDirectory.ShouldBe(Path.Combine(configDirectory, "projects"));
        paths.ClaudeDebugDirectory.ShouldBe(Path.Combine(configDirectory, "debug"));
        paths.ClaudeSettingsFile.ShouldBe(Path.Combine(_localData, "AWL", "config", "claude-settings.json"));
        paths.DefaultClaudeExecutable.ShouldBe(Path.Combine(_profile, ".local", "bin", "claude.exe"));
    }
}
