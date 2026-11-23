using System.IO.Compression;
using AWL.Core.Configuration;
using AWL.Core.Terminal;
using AWL.Infrastructure.Terminal;
using NSubstitute;

namespace AWL.Infrastructure.Tests.Terminal;

public sealed class TerminalLauncherTests
{
    private static readonly ClaudeLaunchSpec Spec = new(@"C:\Program Files\Araç;lar\claude.exe",
        ["--settings", @"C:\Ayarlar; özel\ayar.json"], @"C:\Dava Dosyaları\İş; 42");

    private static readonly string[] ExpectedTerminalArguments = ["-w", "new", "new-tab", "--title", @"Çalışma\; 42",
            "-d", @"C:\Dava Dosyaları\İş\; 42", "--", @"C:\Program Files\Araç\;lar\claude.exe",
            "--settings", @"C:\Ayarlar\; özel\ayar.json"];

    [Fact]
    public void Terminal_preserves_argument_boundaries_and_escapes_command_separators()
    {
        string temp = Path.GetTempPath();
        var terminal = new WindowsTerminalLauncher(new PortableTerminalStep(new AwlPaths(temp, temp, temp)));
        var start = terminal.Build(Spec, "Çalışma; 42");
        start.ArgumentList.ToArray().ShouldBe(ExpectedTerminalArguments);
        start.UseShellExecute.ShouldBeFalse();
    }

    [Fact]
    public void Console_launches_Claude_directly_with_literal_arguments()
    {
        var start = new ConsoleLauncher().Build(Spec, "İş; 42");
        start.FileName.ShouldBe(Spec.Executable);
        start.WorkingDirectory.ShouldBe(Spec.WorkingDirectory);
        start.ArgumentList.ToArray().ShouldBe(Spec.Arguments.ToArray());
        start.UseShellExecute.ShouldBeFalse();
        start.CreateNoWindow.ShouldBeFalse();
        start.RedirectStandardInput.ShouldBeFalse();
        start.RedirectStandardOutput.ShouldBeFalse();
    }

    [Theory]
    [InlineData(TerminalPreference.WindowsConsole, true, false)]
    [InlineData(TerminalPreference.WindowsTerminal, true, true)]
    [InlineData(TerminalPreference.WindowsTerminal, false, false)]
    public async Task Selector_honors_preference_and_availability(TerminalPreference preference, bool available, bool useTerminal)
    {
        var settings = Substitute.For<IAwlSettingsStore>();
        settings.Load().Returns(new AwlSettings { TerminalPreference = preference });
        var terminal = Substitute.For<ITerminalLauncher>();
        terminal.IsAvailable.Returns(available);
        var console = Substitute.For<ITerminalLauncher>();

        await new TerminalLauncherSelector(settings, terminal, console).LaunchAsync(Spec, "başlık", default);
        await (useTerminal ? terminal : console).Received(1).LaunchAsync(Spec, "başlık", default);
        await (useTerminal ? console : terminal).DidNotReceive().LaunchAsync(Spec, "başlık", default);
    }

    [Fact]
    public void Portable_install_retries_preserve_settings_and_restore_missing_executable()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var step = new PortableTerminalStep(new AwlPaths(root.FullName, root.FullName, root.FullName));
            string zip = Path.Combine(root.FullName, "terminal.zip");
            string folder = Path.GetFileName(Path.GetDirectoryName(step.Executable))!;
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                archive.CreateEntry(folder + "/wt.exe");
                archive.CreateEntry(folder + "/WindowsTerminal.exe");
            }
            step.Install(zip, default).IsSuccess.ShouldBeTrue();
            string settings = Path.Combine(Path.GetDirectoryName(step.Executable)!, "settings.json");
            File.WriteAllText(settings, "user");
            File.Delete(step.Executable);
            step.IsInstalled.ShouldBeFalse();
            step.Install(zip, default).IsSuccess.ShouldBeTrue();
            step.IsInstalled.ShouldBeTrue();
            File.ReadAllText(settings).ShouldBe("user");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
