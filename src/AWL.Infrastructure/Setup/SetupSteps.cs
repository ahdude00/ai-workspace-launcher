using AWL.Core.Claude;
using AWL.Core.Common;
using AWL.Core.Configuration;
using AWL.Core.Python;
using AWL.Core.Setup;
using AWL.Infrastructure.Documents;

namespace AWL.Infrastructure.Setup;

internal sealed class ClaudeStep(IClaudeInstallation installation) : ISetupStep
{
    public string Id => "claude";
    public Task<bool> CheckAsync(CancellationToken token) => installation.CheckAsync(token);
    public async Task<Result> RunAsync(CancellationToken token)
    {
        var result = await installation.EnsureInstalledAsync(token).ConfigureAwait(false);
        return result.IsSuccess ? Result.Ok() : Result.Fail(result.Error);
    }
}

internal sealed class PythonStep(IPythonEnvironment python) : ISetupStep
{
    public string Id => "python";
    public Task<bool> CheckAsync(CancellationToken token) => python.CheckAsync(token);
    public Task<Result> RunAsync(CancellationToken token) => python.EnsureReadyAsync(token);
}

internal sealed class ConfigurationStep(AwlPaths paths, IAwlSettingsStore settings) : ISetupStep
{
    public string Id => "configuration";
    public Task<bool> CheckAsync(CancellationToken token) => Task.FromResult(
        Matches(paths.ContextFile, stream => ContextFileWriter.Write(stream, paths)) &&
        Matches(paths.ClaudeSettingsFile, stream => ClaudeSettingsWriter.Write(stream, paths, settings.Load())));

    private static bool Matches(string path, Action<Stream> write)
    {
        using var expected = new MemoryStream();
        write(expected);
        var file = new FileInfo(path);
        return file.Exists && file.Length == expected.Length && File.ReadAllBytes(path).AsSpan().SequenceEqual(expected.ToArray());
    }

    public Task<Result> RunAsync(CancellationToken token)
    {
        settings.RefreshClaudeFiles();
        return Task.FromResult(Result.Ok());
    }
}

internal sealed class UdfStep(AwlPaths paths) : ISetupStep
{
    public string Id => "udf";
    public Task<bool> CheckAsync(CancellationToken token) => Task.FromResult(UdfToolInstaller.IsCurrent(paths));
    public Task<Result> RunAsync(CancellationToken token)
    {
        UdfToolInstaller.EnsureInstalled(paths);
        return Task.FromResult(Result.Ok());
    }
}
