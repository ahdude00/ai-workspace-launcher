using AWL.Core;
using AWL.Core.Common;
using AWL.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AWL.Core.Tests;

public sealed class CoreModuleTests
{
    [Fact]
    public void AddCore_returns_the_same_collection_for_chaining()
    {
        var services = new ServiceCollection();

        services.AddCore().ShouldBeSameAs(services);
    }

    [Fact]
    public void AddCore_rejects_null()
    {
        Should.Throw<ArgumentNullException>(() => CoreModule.AddCore(null!));
    }

    [Fact]
    public void AddCore_registers_the_clock_and_environment_paths()
    {
        var services = new ServiceCollection();
        using ServiceProvider provider = services.AddCore().BuildServiceProvider();

        provider.GetRequiredService<IClock>().ShouldBeOfType<SystemClock>();

        AwlPaths paths = provider.GetRequiredService<AwlPaths>();
        string localData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        string roamingData = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        string profile = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);
        string? claudeConfig = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");

        paths.AwlHome.ShouldBe(Path.Combine(localData, "AWL"));
        paths.RoamingDataDirectory.ShouldBe(Path.Combine(roamingData, "AWL"));
        paths.DefaultClaudeExecutable.ShouldBe(Path.Combine(profile, ".local", "bin", "claude.exe"));
        paths.ClaudeConfigDirectory.ShouldBe(string.IsNullOrWhiteSpace(claudeConfig)
            ? Path.Combine(profile, ".claude")
            : Path.GetFullPath(claudeConfig));
    }
}
