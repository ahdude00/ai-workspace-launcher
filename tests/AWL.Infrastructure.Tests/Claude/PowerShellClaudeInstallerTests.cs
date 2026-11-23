using System.Diagnostics;
using AWL.Core.Configuration;
using AWL.Infrastructure.Claude;
using Microsoft.Extensions.Logging.Abstractions;

namespace AWL.Infrastructure.Tests.Claude;

public sealed class PowerShellClaudeInstallerTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory();
    private readonly AwlPaths _paths;
    private readonly string _pathDirectory;
    private readonly List<ProcessStartInfo> _calls = [];

    public PowerShellClaudeInstallerTests()
    {
        _paths = new AwlPaths(_root.FullName, _root.FullName, _root.FullName);
        _pathDirectory = Directory.CreateDirectory(Path.Combine(_root.FullName, "Kullanıcı Araçları")).FullName;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Existing_installation_is_verified_without_running_the_installer(bool hasUserInstall)
    {
        string pathExecutable = Path.Combine(_pathDirectory, "claude.exe");
        Touch(pathExecutable);
        if (hasUserInstall)
        {
            Touch(_paths.DefaultClaudeExecutable);
        }

        var installer = Create(_ => new CommandOutput(0, "2.1.4 (Claude Code)\r\n", ""));
        var result = await installer.EnsureInstalledAsync(CancellationToken.None);
        result.Value.ShouldBe("2.1.4");
        var call = _calls.ShouldHaveSingleItem();
        call.FileName.ShouldBe(hasUserInstall ? _paths.DefaultClaudeExecutable : pathExecutable);
        call.ArgumentList.ShouldHaveSingleItem().ShouldBe("--version");
        installer.ExecutablePath.ShouldBe(call.FileName);
    }

    [Fact]
    public async Task Missing_installation_runs_official_script_then_probes_the_new_executable()
    {
        var installer = Create(info =>
        {
            if (_calls.Count == 1)
            {
                Path.GetFileName(info.FileName).ShouldBe("powershell.exe");
                info.UseShellExecute.ShouldBeFalse();
                info.ArgumentList.ShouldNotContain("-ExecutionPolicy");
                info.ArgumentList.Last().ShouldContain("https://claude.ai/install.ps1");
                info.ArgumentList.Last().ShouldContain("LanguageMode");
                Touch(_paths.DefaultClaudeExecutable);
                return new CommandOutput(0, "", "");
            }

            info.FileName.ShouldBe(_paths.DefaultClaudeExecutable);
            return new CommandOutput(0, "2.1.4", "");
        });
        (await installer.EnsureInstalledAsync(CancellationToken.None)).Value.ShouldBe("2.1.4");
        _calls.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(85, "ClaudePowerShellRestricted")]
    [InlineData(1, "ClaudeInstallFailed")]
    [InlineData(0, "ClaudeInstallFailed")]
    public async Task Failed_or_incomplete_setup_reports_the_cause(int exitCode, string expected)
    {
        var installer = Create(_ => new CommandOutput(exitCode, "", ""));
        (await installer.EnsureInstalledAsync(CancellationToken.None)).Error!.Code.ShouldBe(expected);
        _calls.Count.ShouldBe(1);
        installer.ExecutablePath.ShouldBeNull();
    }

    [Theory]
    [InlineData(0, "unexpected output")]
    [InlineData(1, "2.1.4")]
    public async Task Invalid_existing_installation_is_reported_without_reinstalling(int code, string output)
    {
        Touch(_paths.DefaultClaudeExecutable);
        var installer = Create(_ => new CommandOutput(code, output, ""));
        (await installer.EnsureInstalledAsync(CancellationToken.None)).Error!.Code.ShouldBe("ClaudeVersionFailed");
        _calls.Count.ShouldBe(1);
    }

    private PowerShellClaudeInstaller Create(Func<ProcessStartInfo, CommandOutput> execute) =>
        new(_paths, NullLogger<PowerShellClaudeInstaller>.Instance, (info, _) =>
        {
            _calls.Add(info);
            return Task.FromResult(execute(info));
        }, () => _pathDirectory);

    private static void Touch(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
    }

    public void Dispose() => _root.Delete(recursive: true);
}
