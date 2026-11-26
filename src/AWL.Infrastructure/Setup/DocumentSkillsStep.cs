using System.Diagnostics;
using System.Text.Json;
using AWL.Core.Claude;
using AWL.Core.Common;
using AWL.Core.Configuration;
using AWL.Core.Setup;
using AWL.Infrastructure.Processes;

namespace AWL.Infrastructure.Setup;

internal sealed class DocumentSkillsStep(IClaudeInstallation claude, AwlPaths paths) : ISetupStep
{
    private const string Marketplace = "anthropic-agent-skills";
    private const string Plugin = "document-skills@" + Marketplace;
    private const string Repository = "anthropics/skills";

    public string Id => "document-skills";
    public bool IsOptional => true;

    public async Task<bool> CheckAsync(CancellationToken token)
    {
        if (claude.ExecutablePath is null)
        {
            return false;
        }
        var list = await RunCliAsync(["plugin", "list", "--json"], token).ConfigureAwait(false);
        if (list is not { ExitCode: 0 })
        {
            return false;
        }
        try
        {
            using var json = JsonDocument.Parse(list.StandardOutput);
            return json.RootElement.ValueKind == JsonValueKind.Array && json.RootElement.EnumerateArray().Any(item =>
                item.ValueKind == JsonValueKind.Object && item.TryGetProperty("id", out var id) && id.ToString() == Plugin &&
                item.TryGetProperty("scope", out var scope) && scope.ToString() == "user" &&
                item.TryGetProperty("enabled", out var enabled) && enabled.ValueKind == JsonValueKind.True);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public async Task<Result> RunAsync(CancellationToken token)
    {
        var unavailable = Result.Fail(new AppError("DocumentSkillsUnavailable", "DocumentSkillsUnavailable"));
        if (claude.ExecutablePath is null)
        {
            return unavailable;
        }
        if (!HasOfficialMarketplace())
        {
            var added = await RunCliAsync(["plugin", "marketplace", "add", Repository], token).ConfigureAwait(false);
            if (added is not { ExitCode: 0 })
            {
                return unavailable;
            }
        }
        var installed = await RunCliAsync(["plugin", "install", Plugin, "--scope", "user"], token).ConfigureAwait(false);
        return installed is { ExitCode: 0 } && await CheckAsync(token).ConfigureAwait(false) ? Result.Ok() : unavailable;
    }

    private bool HasOfficialMarketplace()
    {
        string file = Path.Combine(paths.ClaudeConfigDirectory, "plugins", "known_marketplaces.json");
        if (!File.Exists(file))
        {
            return false;
        }
        try
        {
            using var stream = File.OpenRead(file);
            using var json = JsonDocument.Parse(stream);
            return json.RootElement.ValueKind == JsonValueKind.Object &&
                json.RootElement.TryGetProperty(Marketplace, out var entry) && entry.ValueKind == JsonValueKind.Object &&
                entry.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.Object &&
                source.TryGetProperty("source", out var kind) && kind.ToString() == "github" &&
                source.TryGetProperty("repo", out var repo) && repo.ToString() == Repository;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<CommandOutput?> RunCliAsync(string[] arguments, CancellationToken token)
    {
        var start = new ProcessStartInfo(claude.ExecutablePath!)
        {
            WorkingDirectory = paths.AwlHome,
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
            return await ChildProcess.RunAsync(start, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            return null;
        }
    }
}
