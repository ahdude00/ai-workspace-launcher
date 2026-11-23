using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using AWL.Core.Common;
using AWL.Core.Configuration;
using AWL.Core.Setup;
using AWL.Infrastructure.Processes;

namespace AWL.Infrastructure.Setup;

internal sealed class PreflightStep(AwlPaths paths, IAwlSettingsStore settings) : ISetupStep
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly Uri[] Sources = [
        new("https://claude.ai/install.ps1"), new("https://github.com/astral-sh/python-build-standalone/releases"),
        new("https://pypi.org/simple/"), new("https://files.pythonhosted.org/packages/85/8a/1ddf40be20103bcc605db840e9ade09c8e8c9f920a03e9cfe88eae97a058/pip-25.0-py3-none-any.whl")];

    public string Id => "preflight";
    public Task<bool> CheckAsync(CancellationToken token)
    {
        // Once setup succeeds, a normal offline launch need not probe download servers.
        var steps = settings.Load().SetupSteps;
        return Task.FromResult(steps.GetValueOrDefault(Id) == SetupStepStatus.Completed &&
            steps.GetValueOrDefault("claude") == SetupStepStatus.Completed &&
            steps.GetValueOrDefault("python") == SetupStepStatus.Completed);
    }

    public async Task<Result> RunAsync(CancellationToken token)
    {
        var findings = new List<string>();
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763) || RuntimeInformation.OSArchitecture != Architecture.X64)
        {
            findings.Add($"Windows 10 1809 veya üzeri, x64 gerekir. Sistem: {Environment.OSVersion.Version}, {RuntimeInformation.OSArchitecture}.");
        }
        foreach (var directory in new[] { paths.AwlHome, paths.RoamingDataDirectory, paths.ConfigDirectory, paths.ToolsDirectory })
        {
            try
            {
                Directory.CreateDirectory(directory);
                using var probe = new FileStream(Path.Combine(directory, Path.GetRandomFileName()), FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
                probe.WriteByte(1);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                findings.Add($"AWL klasörüne yazılamıyor ({Path.GetFileName(directory)}, 0x{ex.HResult:X8}). Yazma iznini kontrol edin.");
            }
        }
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            ArgumentList = { "-NoProfile", "-NonInteractive", "-Command", "$ExecutionContext.SessionState.LanguageMode" },
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var output = await ChildProcess.RunAsync(info, timeout.Token).ConfigureAwait(false);
            if (output.ExitCode != 0 || output.StandardOutput.Trim() != "FullLanguage")
            {
                findings.Add("PowerShell tam dil modunda çalışmıyor. Kurulum için BT ekibinizle görüşün.");
            }
        }
        catch (Exception ex) when (!token.IsCancellationRequested && ex is Win32Exception or IOException or OperationCanceledException or UnauthorizedAccessException)
        {
            findings.Add($"PowerShell çalıştırılamadı (0x{ex.HResult:X8}). Uygulama izinlerini BT ekibinize kontrol ettirin.");
        }
        var network = await Task.WhenAll(Sources.Select(uri => CheckSourceAsync(uri, token))).ConfigureAwait(false);
        findings.AddRange(network.OfType<string>());
        return findings.Count == 0 ? Result.Ok() :
            Result.Fail(new AppError("SetupPreflightFailed", "SetupPreflightFailed", string.Join(Environment.NewLine, findings)));
    }

    private static async Task<string?> CheckSourceAsync(Uri uri, CancellationToken token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return null;
            }
            return $"{uri.Host} erişimi başarısız (HTTP {(int)response.StatusCode}). Ağ ve proxy ayarlarını kontrol edin.";
        }
        catch (Exception ex) when (!token.IsCancellationRequested && ex is HttpRequestException or OperationCanceledException)
        {
            return $"{uri.Host} adresine ulaşılamadı (0x{ex.HResult:X8}). Ağ ve proxy ayarlarını kontrol edin.";
        }
    }
}
