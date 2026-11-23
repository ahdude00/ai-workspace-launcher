using AWL.Core.Claude;
using AWL.Core.Configuration;
using AWL.Core.Sessions;

namespace AWL.Core.Tests.Claude;

public sealed class ClaudeCommandBuilderTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "Çağrı Şen");
    private static readonly AwlPaths Paths = new(Root, Root, Root);
    private static readonly Session Session = new(
        Guid.NewGuid(), "Dava", null, Path.Combine(Root, "İş Hukuku"), Guid.NewGuid(),
        null, false, DateTimeOffset.UnixEpoch, null, null);

    [Theory]
    [InlineData(LaunchMode.New, "--session-id")]
    [InlineData(LaunchMode.Resume, "--resume")]
    public void Build_preserves_argument_boundaries_and_uses_the_Claude_session_id(LaunchMode mode, string flag)
    {
        var spec = new ClaudeCommandBuilder(Paths).Build(Session, mode);

        spec.Executable.ShouldBe(Paths.DefaultClaudeExecutable);
        spec.WorkingDirectory.ShouldBe(Session.WorkspacePath);
        spec.Arguments.ToArray().ShouldBe(new[]
        {
            flag, Session.ClaudeSessionId.ToString("D"),
            "--settings", Paths.ClaudeSettingsFile,
            "--append-system-prompt-file", Paths.ContextFile,
        });
    }

    [Fact]
    public void Build_uses_the_discovered_executable_when_supplied()
    {
        string executable = Path.Combine(Root, "Özel Kurulum", "claude.exe");
        new ClaudeCommandBuilder(Paths).Build(Session, LaunchMode.Resume, executable).Executable.ShouldBe(executable);
    }

    [Fact]
    public void Build_rejects_an_unknown_mode()
    {
        var builder = new ClaudeCommandBuilder(Paths);
        Should.Throw<ArgumentOutOfRangeException>(() => builder.Build(Session, (LaunchMode)32));
    }
}
