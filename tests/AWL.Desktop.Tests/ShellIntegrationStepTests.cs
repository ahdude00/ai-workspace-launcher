using System.Security;
using AWL.Core.Shell;
using AWL.Desktop.Resources;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AWL.Desktop.Tests;

public sealed class ShellIntegrationStepTests
{
    private const string Exe = @"C:\Programlar\AWL\AWL.exe";
    private readonly IShellIntegration _shell = Substitute.For<IShellIntegration>();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Check_compares_with_the_running_executable(bool registered)
    {
        _shell.IsRegistered(Exe, Strings.ShellMenuText).Returns(registered);
        var step = new ShellIntegrationStep(_shell, () => Exe);

        step.Id.ShouldBe("shell");
        (await step.CheckAsync(default)).ShouldBe(registered);
    }

    [Fact]
    public async Task Run_registers_the_running_executable()
    {
        (await new ShellIntegrationStep(_shell, () => Exe).RunAsync(default)).IsSuccess.ShouldBeTrue();
        _shell.Received(1).Register(Exe, Strings.ShellMenuText);
    }

    [Fact]
    public async Task A_policy_block_is_reported_not_bypassed()
    {
        _shell.When(s => s.Register(Arg.Any<string>(), Arg.Any<string>())).Throw(new SecurityException());

        var result = await new ShellIntegrationStep(_shell, () => Exe).RunAsync(default);

        result.Error!.Code.ShouldBe("ShellIntegrationFailed");
    }

    [Fact]
    public async Task An_unknown_executable_path_fails_without_registering()
    {
        var step = new ShellIntegrationStep(_shell, () => null);

        (await step.CheckAsync(default)).ShouldBeFalse();
        (await step.RunAsync(default)).Error!.Code.ShouldBe("ShellIntegrationFailed");
        _shell.DidNotReceive().Register(Arg.Any<string>(), Arg.Any<string>());
    }
}
