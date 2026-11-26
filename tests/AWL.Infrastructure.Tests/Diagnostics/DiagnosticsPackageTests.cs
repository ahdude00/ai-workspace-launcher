using System.IO.Compression;
using AWL.Core.Configuration;
using AWL.Core.Diagnostics;
using AWL.Infrastructure.Diagnostics;
using AWL.Infrastructure.Python;
using NSubstitute;

namespace AWL.Infrastructure.Tests.Diagnostics;

public sealed class DiagnosticsPackageTests : IDisposable
{
    private const string SessionTitle = "Gizli Dava Başlığı";
    private const string DocumentText = "Belgenin gizli içeriği";
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("awl-diag-");
    private readonly AwlPaths _paths;
    private readonly IAwlSettingsStore _settings = Substitute.For<IAwlSettingsStore>();
    private readonly VersionReport _versions = new("1.2.3", "2.1.4", "3.13.1", "Microsoft Windows NT 10.0.19045.0", "X64");

    public DiagnosticsPackageTests()
    {
        _paths = new AwlPaths(Path.Combine(_root.FullName, "local"), Path.Combine(_root.FullName, "roaming"),
            Path.Combine(_root.FullName, "profile"));
        _settings.Load().Returns(new AwlSettings
        {
            RetentionDays = 90,
            SetupSteps = new Dictionary<string, SetupStepStatus>
            {
                ["preflight"] = SetupStepStatus.Completed,
                ["python"] = SetupStepStatus.Failed,
            },
        });
        Directory.CreateDirectory(_paths.LogsDirectory);
        Directory.CreateDirectory(_paths.RoamingDataDirectory);
        File.WriteAllText(Path.Combine(_paths.LogsDirectory, "awl.log"), "2026-11-26 [INF] AWL.App: started\n");
        File.WriteAllText(Path.Combine(_paths.LogsDirectory, "awl_001.log"), "older\n");
        File.WriteAllText(Path.Combine(_paths.LogsDirectory, "setup-diagnostics.zip"), "zip");
        // Data that must never leave the computer through the package.
        File.WriteAllText(Path.Combine(_paths.LogsDirectory, "other.log"), DocumentText);
        File.WriteAllText(Path.Combine(_paths.LogsDirectory, "awl_notes.log"), SessionTitle);
        File.WriteAllText(_paths.DatabaseFile, SessionTitle);
        File.WriteAllText(_paths.SettingsFile, SessionTitle);
        File.WriteAllText(Path.Combine(_root.FullName, "Dilekçe.docx"), DocumentText);
    }

    private DiagnosticsPackage Create() => new(_paths, _settings);

    private string Target => Path.Combine(_root.FullName, "Masaüstü; destek", "AWL-tanılama.zip");

    [Fact]
    public async Task The_package_holds_versions_setup_results_and_logs_only()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Target)!);

        (await Create().SaveAsync(Target, _versions, default)).IsSuccess.ShouldBeTrue();

        using var archive = ZipFile.OpenRead(Target);
        archive.Entries.Select(e => e.FullName).ShouldBe(["bilgi.txt", "gunlukler/awl.log", "gunlukler/awl_001.log"]);
        string info = Read(archive, "bilgi.txt");
        info.ShouldContain("AWL: 1.2.3");
        info.ShouldContain("Claude Code: 2.1.4");
        info.ShouldContain("Python: 3.13.1");
        info.ShouldContain("Windows: Microsoft Windows NT 10.0.19045.0 (X64)");
        info.ShouldContain("preflight: Completed");
        info.ShouldContain("python: Failed");
        Read(archive, "gunlukler/awl.log").ShouldContain("AWL.App: started");
        foreach (var entry in archive.Entries)
        {
            string text = Read(archive, entry.FullName);
            text.ShouldNotContain(SessionTitle);
            text.ShouldNotContain(DocumentText);
            text.ShouldNotContain(_root.FullName);
        }
    }

    [Fact]
    public async Task Missing_versions_are_marked_and_an_open_log_can_be_read()
    {
        string log = Path.Combine(_paths.LogsDirectory, "awl.log");
        using var writer = new FileStream(log, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        Directory.CreateDirectory(Path.GetDirectoryName(Target)!);

        (await Create().SaveAsync(Target, _versions with { Claude = null, Python = null }, default)).IsSuccess.ShouldBeTrue();

        using var archive = ZipFile.OpenRead(Target);
        Read(archive, "bilgi.txt").ShouldContain("Claude Code: -");
        Read(archive, "bilgi.txt").ShouldContain("Python: -");
    }

    [Fact]
    public async Task An_existing_file_is_replaced_and_no_temporary_file_remains()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Target)!);
        await File.WriteAllTextAsync(Target, "old");

        (await Create().SaveAsync(Target, _versions, default)).IsSuccess.ShouldBeTrue();

        using (ZipFile.OpenRead(Target))
        {
        }
        Directory.GetFiles(Path.GetDirectoryName(Target)!).ShouldBe([Target]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("göreli.zip")]
    [InlineData("C:/tanılama.txt")]
    public async Task Invalid_destinations_are_refused(string destination)
    {
        (await Create().SaveAsync(destination, _versions, default)).Error!.Code.ShouldBe("DiagnosticsSaveFailed");
    }

    [Fact]
    public async Task A_missing_folder_is_reported()
    {
        (await Create().SaveAsync(Path.Combine(_root.FullName, "yok", "x.zip"), _versions, default)).Error!.Code
            .ShouldBe("DiagnosticsSaveFailed");
    }

    [Fact]
    public async Task Without_logs_the_package_still_has_the_information_file()
    {
        Directory.Delete(_paths.LogsDirectory, recursive: true);
        Directory.CreateDirectory(Path.GetDirectoryName(Target)!);

        (await Create().SaveAsync(Target, _versions, default)).IsSuccess.ShouldBeTrue();

        using var archive = ZipFile.OpenRead(Target);
        archive.Entries.ShouldHaveSingleItem().FullName.ShouldBe("bilgi.txt");
    }

    [Theory]
    [InlineData("home = C:\\x\nversion_info = 3.13.1\nversion = 3.13\n", "3.13.1")]
    [InlineData("version = 3.13.0\n", "3.13.0")]
    [InlineData("home = C:\\x\n", null)]
    [InlineData("version_info = 3.13; rm -rf\n", null)]
    public void Python_version_comes_from_pyvenv_cfg(string content, string? expected)
    {
        Directory.CreateDirectory(_paths.VirtualEnvironmentDirectory);
        File.WriteAllText(Path.Combine(_paths.VirtualEnvironmentDirectory, "pyvenv.cfg"), content);

        PythonVersion.Read(_paths).ShouldBe(expected);
    }

    [Fact]
    public void Without_an_environment_there_is_no_python_version()
    {
        PythonVersion.Read(_paths).ShouldBeNull();
    }

    private static string Read(ZipArchive archive, string name)
    {
        using var reader = new StreamReader(archive.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }

    public void Dispose() => _root.Delete(recursive: true);
}
