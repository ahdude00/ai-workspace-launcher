using AWL.Core.Configuration;
using AWL.Infrastructure.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AWL.Infrastructure.Tests.Configuration;

public sealed class AwlSettingsStoreTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory();
    private readonly AwlPaths _paths;
    private readonly ServiceProvider _provider;
    private readonly IAwlSettingsStore _store;

    public AwlSettingsStoreTests()
    {
        _paths = new AwlPaths(_root.FullName, _root.FullName, _root.FullName);
        var services = new ServiceCollection();
        services.AddSingleton(_paths);
        services.AddLogging();
        services.AddSingleton<ILoggerProvider>(_ => FileLogging.CreateProvider(_paths, new LoggingOptions()));
        _provider = services.AddInfrastructure().BuildServiceProvider();
        _store = _provider.GetRequiredService<IAwlSettingsStore>();
    }

    [Fact]
    public void Missing_file_returns_defaults()
    {
        var settings = _store.Load();
        settings.RetentionDays.ShouldBe(3650);
        settings.TerminalPreference.ShouldBe(TerminalPreference.WindowsTerminal);
        settings.SetupSteps.ShouldBeEmpty();
        File.Exists(_paths.SettingsFile).ShouldBeFalse();
    }

    [Fact]
    public void Save_replaces_previous_settings_and_round_trips_step_states()
    {
        _store.Save(new AwlSettings());
        _store.Save(new AwlSettings
        {
            RetentionDays = 90,
            TerminalPreference = TerminalPreference.WindowsConsole,
            SetupSteps = new Dictionary<string, SetupStepStatus> { ["python"] = SetupStepStatus.Completed },
        });

        var settings = _store.Load();
        settings.RetentionDays.ShouldBe(90);
        settings.TerminalPreference.ShouldBe(TerminalPreference.WindowsConsole);
        settings.SetupSteps["python"].ShouldBe(SetupStepStatus.Completed);
        Directory.GetFiles(_paths.RoamingDataDirectory).ShouldBe([_paths.SettingsFile]);
    }

    [Theory]
    [InlineData("private-content")]
    [InlineData("null")]
    [InlineData("""{"retentionDays":-1}""")]
    [InlineData("""{"terminalPreference":"private-content"}""")]
    [InlineData("""{"setupSteps":null}""")]
    public void Invalid_file_returns_defaults_and_logs_only_a_warning(string content)
    {
        Directory.CreateDirectory(_paths.RoamingDataDirectory);
        File.WriteAllText(_paths.SettingsFile, content);

        _store.Load().RetentionDays.ShouldBe(3650);
        var log = File.ReadAllText(Directory.GetFiles(_paths.LogsDirectory).Single());
        log.ShouldContain("Invalid AWL settings");
        log.ShouldNotContain("private-content");
        File.ReadAllText(_paths.SettingsFile).ShouldBe(content);
    }

    [Fact]
    public void Invalid_save_preserves_previous_file()
    {
        _store.Save(new AwlSettings { RetentionDays = 42 });
        var invalid = new AwlSettings { RetentionDays = 0 };
        Should.Throw<ArgumentException>(() => _store.Save(invalid));
        _store.Load().RetentionDays.ShouldBe(42);

        Directory.GetFiles(_paths.RoamingDataDirectory).ShouldBe([_paths.SettingsFile]);
    }

    [Fact]
    public void Concurrent_replacements_leave_only_complete_snapshots()
    {
        _store.Save(new AwlSettings { RetentionDays = 1 });
        Parallel.For(1, 32, days =>
        {
            _store.Save(new AwlSettings { RetentionDays = days });
            _store.Load().RetentionDays.ShouldBeInRange(1, 31);
        });
        Directory.GetFiles(_paths.RoamingDataDirectory).ShouldBe([_paths.SettingsFile]);
    }

    public void Dispose()
    {
        _provider.Dispose();
        _root.Delete(recursive: true);
    }
}
