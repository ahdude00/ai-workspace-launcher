using AWL.Core.Claude;
using AWL.Core.Configuration;
using AWL.Core.Diagnostics;
using AWL.Core.Python;
using AWL.Desktop.Resources;
using AWL.Desktop.Services;
using AWL.Desktop.ViewModels;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AWL.Desktop.Tests.ViewModels;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly SessionFixture _fixture = new();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IRepairService _repair = Substitute.For<IRepairService>();
    private readonly IDiagnosticsPackage _package = Substitute.For<IDiagnosticsPackage>();
    private readonly List<AwlSettings> _saved = [];
    private int _closed;

    public SettingsViewModelTests()
    {
        _fixture.RetentionDays = 3650;
        _fixture.Settings.When(s => s.Save(Arg.Any<AwlSettings>())).Do(call => _saved.Add(call.Arg<AwlSettings>()));
    }

    private SettingsViewModel Create()
    {
        var settings = new SettingsViewModel(_fixture.Settings, _fixture.Deletion, _dialogs, _repair, _package,
            new AppVersion("1.0.0"), Substitute.For<IClaudeInstallation>(), Substitute.For<IPythonEnvironment>());
        settings.CloseRequested += (_, _) => _closed++;
        return settings;
    }

    [Fact]
    public void Shows_the_saved_values()
    {
        var settings = Create();

        settings.RetentionText.ShouldBe("3650");
        settings.UseWindowsTerminal.ShouldBeTrue();
        settings.UseConsole.ShouldBeFalse();
    }

    [Fact]
    public async Task Saving_keeps_setup_state_and_writes_both_settings()
    {
        _fixture.Settings.Load().Returns(new AwlSettings
        {
            RetentionDays = 3650,
            SetupSteps = new Dictionary<string, SetupStepStatus> { ["login"] = SetupStepStatus.Completed },
        });
        var settings = Create();
        settings.RetentionText = " 7300 ";
        settings.UseConsole = true;

        await settings.SaveCommand.ExecuteAsync(null);

        var saved = _saved.ShouldHaveSingleItem();
        saved.RetentionDays.ShouldBe(7300);
        saved.TerminalPreference.ShouldBe(TerminalPreference.WindowsConsole);
        saved.SetupSteps["login"].ShouldBe(SetupStepStatus.Completed);
        _closed.ShouldBe(1);
        _dialogs.DidNotReceive().Confirm(Arg.Any<string>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("36501")]
    [InlineData("on")]
    [InlineData("3.5")]
    public async Task Invalid_periods_are_explained_and_not_saved(string text)
    {
        var settings = Create();
        settings.RetentionText = text;

        await settings.SaveCommand.ExecuteAsync(null);

        settings.ErrorMessage.ShouldBe(Strings.SettingsRetentionInvalid);
        _saved.ShouldBeEmpty();
        _closed.ShouldBe(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Shortening_the_period_asks_before_deleting_sessions(bool confirm)
    {
        await _fixture.AddAsync("Eski", lastActivity: _fixture.Now.AddDays(-60));
        await _fixture.AddAsync("Yeni", lastActivity: _fixture.Now.AddDays(-2));
        _dialogs.Confirm(Arg.Any<string>()).Returns(confirm);
        var settings = Create();
        settings.RetentionText = "30";

        await settings.SaveCommand.ExecuteAsync(null);

        _dialogs.Received(1).Confirm(Arg.Is<string>(text => text.Contains('1', StringComparison.Ordinal)
            && text.Contains("30", StringComparison.Ordinal)));
        _fixture.Repository.Items.Count.ShouldBe(confirm ? 1 : 2);
        _saved.Count.ShouldBe(confirm ? 1 : 0);
    }

    [Fact]
    public async Task A_failed_save_is_reported()
    {
        _fixture.Settings.When(s => s.Save(Arg.Any<AwlSettings>())).Throw(new UnauthorizedAccessException());
        var settings = Create();

        await settings.SaveCommand.ExecuteAsync(null);

        settings.ErrorMessage.ShouldBe(Strings.SettingsSaveFailed);
        _closed.ShouldBe(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Repair_asks_then_closes_and_starts(bool confirm)
    {
        _dialogs.Confirm(Strings.RepairConfirm).Returns(confirm);

        Create().RepairCommand.Execute(null);

        _closed.ShouldBe(confirm ? 1 : 0);
        _repair.Received(confirm ? 1 : 0).RequestRepair();
    }

    [Fact]
    public async Task Support_commands_reach_diagnostics_and_about()
    {
        var settings = Create();
        _dialogs.PickSaveFile(Arg.Any<string>(), Arg.Any<string>()).Returns((string?)null);

        await settings.SaveDiagnosticsCommand.ExecuteAsync(null);
        settings.ShowAboutCommand.Execute(null);

        _dialogs.Received(1).PickSaveFile(Arg.Any<string>(), Strings.DiagnosticsFileFilter);
        _dialogs.Received(1).ShowAbout();
    }

    public void Dispose() => _fixture.Dispose();
}
