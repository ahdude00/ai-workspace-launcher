using System.Diagnostics;
using AWL.Core.Configuration;
using AWL.Infrastructure.Processes;
using AWL.Infrastructure.Python;
using Microsoft.Extensions.Logging.Abstractions;

namespace AWL.Infrastructure.Tests.Python;

public sealed class UvPythonEnvironmentTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("awl python ");
    private readonly AwlPaths _paths;
    private readonly List<ProcessStartInfo> _commands = [];

    public UvPythonEnvironmentTests() =>
        _paths = new AwlPaths(_root.FullName, _root.FullName, _root.FullName);

    [Fact]
    public async Task Clean_setup_targets_the_managed_runtime_and_skips_a_second_install()
    {
        var environment = CreateEnvironment(info =>
        {
            if (info.ArgumentList[0] == "venv")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_paths.PythonExecutable)!);
                File.WriteAllText(_paths.PythonExecutable, "");
            }

            return 0;
        });

        (await environment.EnsureReadyAsync(CancellationToken.None)).IsSuccess.ShouldBeTrue();
        _commands.Select(info => info.ArgumentList[0]).ToArray().ShouldBe(["python", "venv", "pip", "-I"]);
        foreach (var info in _commands.Take(3))
        {
            Path.IsPathFullyQualified(info.FileName).ShouldBeTrue();
            Path.GetFileName(info.FileName).ShouldBe("uv.exe");
            info.Environment["UV_PYTHON_INSTALL_DIR"].ShouldBe(_paths.PythonInstallDirectory);
            info.Environment["UV_CACHE_DIR"].ShouldBe(_paths.UvCacheDirectory);
            info.Environment["UV_NATIVE_TLS"].ShouldBe("1");
            info.Environment["PATH"].ShouldBe(Environment.GetEnvironmentVariable("PATH"));
        }

        _commands[0].ArgumentList.ShouldContain("--no-bin");
        _commands[0].ArgumentList.ShouldContain("--no-registry");
        _commands[1].ArgumentList.ShouldContain("--seed");
        _commands[1].ArgumentList.ShouldContain("--managed-python");
        _commands[2].ArgumentList.ShouldContain("--require-hashes");
        _commands[2].ArgumentList.ShouldContain(_paths.PythonExecutable);
        File.Exists(_commands[2].ArgumentList[^2]).ShouldBeTrue();
        File.Exists(_commands[3].ArgumentList[1]).ShouldBeTrue();

        _commands.Clear();
        (await environment.EnsureReadyAsync(CancellationToken.None)).IsSuccess.ShouldBeTrue();
        _commands.ShouldHaveSingleItem().FileName.ShouldBe(_paths.PythonExecutable);
    }

    [Fact]
    public async Task Missing_packages_are_repaired_without_replacing_the_venv()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_paths.PythonExecutable)!);
        File.WriteAllText(_paths.PythonExecutable, "");
        var environment = CreateEnvironment(_ => _commands.Count == 1 ? 1 : 0);
        (await environment.EnsureReadyAsync(CancellationToken.None)).IsSuccess.ShouldBeTrue();
        _commands.Select(info => info.ArgumentList[0]).ToArray().ShouldBe(["-I", "pip", "-I"]);
    }

    [Fact]
    public async Task Failed_installation_is_reported_and_can_be_retried()
    {
        var environment = CreateEnvironment(_ => 1);
        (await environment.EnsureReadyAsync(CancellationToken.None)).IsSuccess.ShouldBeFalse();
        (await environment.EnsureReadyAsync(CancellationToken.None)).IsSuccess.ShouldBeFalse();
        _commands.Count.ShouldBe(2);
        _commands.ShouldAllBe(info => info.ArgumentList[0] == "python");
    }

    private UvPythonEnvironment CreateEnvironment(Func<ProcessStartInfo, int> exitCode) =>
        new(_paths, NullLogger<UvPythonEnvironment>.Instance, (info, _) =>
        {
            _commands.Add(info);
            return Task.FromResult(new CommandOutput(exitCode(info), "", ""));
        });

    public void Dispose() => _root.Delete(recursive: true);
}
