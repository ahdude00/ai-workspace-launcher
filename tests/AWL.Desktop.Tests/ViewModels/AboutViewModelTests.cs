using AWL.Core.Claude;
using AWL.Core.Common;
using AWL.Core.Diagnostics;
using AWL.Core.Python;
using AWL.Desktop.Resources;
using AWL.Desktop.Services;
using AWL.Desktop.ViewModels;
using NSubstitute;

namespace AWL.Desktop.Tests.ViewModels;

public sealed class AboutViewModelTests
{
    private readonly IClaudeInstallation _claude = Substitute.For<IClaudeInstallation>();
    private readonly IPythonEnvironment _python = Substitute.For<IPythonEnvironment>();
    private readonly IDiagnosticsPackage _package = Substitute.For<IDiagnosticsPackage>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    private AboutViewModel Create() => new(new AppVersion("1.0.0+abc"), _claude, _python, _package, _dialogs);

    [Fact]
    public void Shows_every_version_and_the_licenses()
    {
        _claude.Version.Returns("2.1.4");
        _python.InstalledVersion.Returns("3.13.1");

        var about = Create();

        about.AwlVersion.ShouldBe("1.0.0+abc");
        about.ClaudeVersion.ShouldBe("2.1.4");
        about.PythonVersion.ShouldBe("3.13.1");
        about.WindowsVersion.ShouldContain(Environment.OSVersion.VersionString);
        about.Notices.ShouldContain("uv");
        about.Notices.ShouldContain("Windows Terminal");
        about.Notices.ShouldContain("LIS-01");
    }

    [Fact]
    public void Unknown_versions_are_explained()
    {
        _claude.Version.Returns((string?)null);
        _python.InstalledVersion.Returns((string?)null);

        var about = Create();

        about.ClaudeVersion.ShouldBe(Strings.VersionUnknown);
        about.PythonVersion.ShouldBe(Strings.VersionNotInstalled);
    }

    [Fact]
    public async Task Saving_asks_where_and_reports_the_location()
    {
        _dialogs.PickSaveFile(Arg.Any<string>(), Arg.Any<string>()).Returns(@"C:\Masaüstü\AWL.zip");
        _package.SaveAsync(Arg.Any<string>(), Arg.Any<VersionReport>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());
        var about = Create();

        await about.SaveDiagnosticsCommand.ExecuteAsync(null);

        _dialogs.Received(1).PickSaveFile(Arg.Is<string>(n => n.StartsWith("AWL-tanilama-", StringComparison.Ordinal)
            && n.EndsWith(".zip", StringComparison.Ordinal)), Strings.DiagnosticsFileFilter);
        await _package.Received(1).SaveAsync(@"C:\Masaüstü\AWL.zip", about.Versions, Arg.Any<CancellationToken>());
        _dialogs.Received(1).ShowInfo(Arg.Is<string>(text => text.Contains(@"C:\Masaüstü\AWL.zip", StringComparison.Ordinal)));
        about.IsSaving.ShouldBeFalse();
    }

    [Fact]
    public async Task Cancelling_saves_nothing_and_a_failure_is_reported()
    {
        var about = Create();
        _dialogs.PickSaveFile(Arg.Any<string>(), Arg.Any<string>()).Returns((string?)null);
        await about.SaveDiagnosticsCommand.ExecuteAsync(null);
        await _package.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<VersionReport>(), Arg.Any<CancellationToken>());

        _dialogs.PickSaveFile(Arg.Any<string>(), Arg.Any<string>()).Returns(@"C:\x.zip");
        _package.SaveAsync(Arg.Any<string>(), Arg.Any<VersionReport>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail(new AppError("DiagnosticsSaveFailed", "DiagnosticsSaveFailed")));
        await about.SaveDiagnosticsCommand.ExecuteAsync(null);
        _dialogs.Received(1).ShowError(Strings.DiagnosticsSaveFailed);
    }
}
