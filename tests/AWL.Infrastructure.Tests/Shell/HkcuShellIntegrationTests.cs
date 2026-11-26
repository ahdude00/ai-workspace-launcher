using AWL.Core.Shell;
using AWL.Infrastructure.Shell;
using Microsoft.Win32;

namespace AWL.Infrastructure.Tests.Shell;

/// <summary>Runs against a private key below HKCU\Software\AWL.Tests and removes it afterwards.</summary>
public sealed class HkcuShellIntegrationTests : IDisposable
{
    private const string Text = "AWL ile aç";
    private readonly string _testRoot = @"Software\AWL.Tests\" + Guid.NewGuid().ToString("N");
    private readonly string _exe = @"C:\Users\Çağrı Şen\AppData\Local\Programs\AWL; test\AWL.exe";

    private HkcuShellIntegration Create() => new() { ClassesRoot = _testRoot + @"\Classes" };

    [Fact]
    public void Register_writes_every_entry_and_is_detected()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var shell = Create();
        shell.IsRegistered(_exe, Text).ShouldBeFalse();

        shell.Register(_exe, Text);

        shell.IsRegistered(_exe, Text).ShouldBeTrue();
        foreach (var entry in ShellMenu.Entries(_exe))
        {
            using var verb = Registry.CurrentUser.OpenSubKey($@"{_testRoot}\Classes\{entry.VerbKey}");
            verb!.GetValue("MUIVerb").ShouldBe(Text);
            verb.GetValue("Icon").ShouldBe(ShellMenu.Icon(_exe));
            using var command = verb.OpenSubKey("command");
            command!.GetValue(null).ShouldBe(entry.Command);
        }
    }

    [Fact]
    public void A_moved_executable_or_other_text_needs_registering_again()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var shell = Create();
        shell.Register(_exe, Text);

        shell.IsRegistered(@"D:\Başka\AWL.exe", Text).ShouldBeFalse();
        shell.IsRegistered(_exe, "Open with AWL").ShouldBeFalse();
        shell.Register(@"D:\Başka\AWL.exe", Text);
        shell.IsRegistered(@"D:\Başka\AWL.exe", Text).ShouldBeTrue();
    }

    [Fact]
    public void Unregister_removes_only_AWL_keys_and_can_repeat()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var shell = Create();
        using (var other = Registry.CurrentUser.CreateSubKey($@"{_testRoot}\Classes\Directory\shell\OtherApp\command"))
        {
            other.SetValue(null, "other.exe \"%1\"");
        }
        shell.Register(_exe, Text);

        shell.Unregister();
        shell.Unregister();

        shell.IsRegistered(_exe, Text).ShouldBeFalse();
        foreach (var entry in ShellMenu.Entries(_exe))
        {
            Registry.CurrentUser.OpenSubKey($@"{_testRoot}\Classes\{entry.VerbKey}").ShouldBeNull();
        }
        using var kept = Registry.CurrentUser.OpenSubKey($@"{_testRoot}\Classes\Directory\shell\OtherApp\command");
        kept!.GetValue(null).ShouldBe("other.exe \"%1\"");
    }

    public void Dispose()
    {
        if (OperatingSystem.IsWindows())
        {
            Registry.CurrentUser.DeleteSubKeyTree(_testRoot, throwOnMissingSubKey: false);
        }
    }
}
