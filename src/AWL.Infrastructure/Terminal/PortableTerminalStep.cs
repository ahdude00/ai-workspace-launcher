using System.IO.Compression;
using AWL.Core.Common;
using AWL.Core.Configuration;
using AWL.Core.Setup;

namespace AWL.Infrastructure.Terminal;

internal sealed class PortableTerminalStep(AwlPaths paths) : ISetupStep
{
    // A versioned directory lets already-running terminals continue during upgrades.
    private const string DirectoryName = "terminal-1.25.3131.0";
    private string DirectoryPath => Path.Combine(paths.ToolsDirectory, DirectoryName);
    internal string Executable => Path.Combine(DirectoryPath, "wt.exe");
    internal static bool IsSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041);
    internal bool IsInstalled => IsComplete(DirectoryPath);
    public string Id => "windows-terminal";

    private static bool IsComplete(string directory) => File.Exists(Path.Combine(directory, ".portable"))
        && File.Exists(Path.Combine(directory, "WindowsTerminal.exe")) && File.Exists(Path.Combine(directory, "wt.exe"));

    public Task<bool> CheckAsync(CancellationToken token) => Task.FromResult(!IsSupported || IsInstalled);

    public Task<Result> RunAsync(CancellationToken token) => Task.Run(() =>
        Install(Path.Combine(AppContext.BaseDirectory, "third_party", "windows-terminal.zip"), token), token);

    internal Result Install(string archive, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (IsInstalled)
        {
            return Result.Ok();
        }
        Directory.CreateDirectory(paths.ToolsDirectory);
        string staging = Directory.CreateDirectory(Path.Combine(paths.ToolsDirectory, Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            ZipFile.ExtractToDirectory(archive, staging);
            string source = Path.Combine(staging, DirectoryName);
            File.WriteAllText(Path.Combine(source, ".portable"), "");
            if (!IsComplete(source))
            {
                return Failed();
            }
            token.ThrowIfCancellationRequested();
            try
            {
                Directory.Move(source, DirectoryPath);
            }
            catch (IOException) when (Directory.Exists(DirectoryPath))
            {
                // Repair missing package files without replacing the user's portable settings.
                foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                {
                    token.ThrowIfCancellationRequested();
                    string target = Path.Combine(DirectoryPath, Path.GetRelativePath(source, file));
                    if (!File.Exists(target))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        try
                        {
                            File.Move(file, target);
                        }
                        catch (IOException) when (File.Exists(target))
                        {
                            // Another setup restored this file first.
                        }
                    }
                }
            }
            return Result.Ok();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return Failed();
        }
        finally
        {
            Directory.Delete(staging, recursive: true);
        }
    }

    private static Result Failed() => Result.Fail(new AppError("TerminalSetupFailed", "TerminalSetupFailed"));
}
