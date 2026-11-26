using System.Globalization;
using System.IO.Compression;
using AWL.Core.Common;
using AWL.Core.Configuration;
using AWL.Core.Diagnostics;

namespace AWL.Infrastructure.Diagnostics;

/// <summary>Writes the support ZIP: an information file and AWL's own log files, nothing else.</summary>
public sealed class DiagnosticsPackage(AwlPaths paths, IAwlSettingsStore settings, TimeProvider? time = null)
    : IDiagnosticsPackage
{
    /// <summary>Log files larger than this are cut to their newest part; a support package stays small.</summary>
    internal const long MaxLogBytes = 20 * 1024 * 1024;

    private static readonly AppError Failed = new("DiagnosticsSaveFailed", "DiagnosticsSaveFailed");

    public Task<Result> SaveAsync(string destination, VersionReport versions, CancellationToken cancellationToken) =>
        Task.Run(() => Save(destination, versions, cancellationToken), cancellationToken);

    private Result Save(string destination, VersionReport versions, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(versions);
        if (string.IsNullOrWhiteSpace(destination) || !Path.IsPathFullyQualified(destination)
            || !destination.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail(Failed with { Detail = "destination" });
        }

        string directory = Path.GetDirectoryName(Path.GetFullPath(destination))!;
        string temporary = Path.Combine(directory, $".awl-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
            {
                WriteInformation(archive, versions);
                foreach (string log in LogFiles())
                {
                    token.ThrowIfCancellationRequested();
                    CopyLog(archive, log);
                }
            }
            File.Move(temporary, destination, overwrite: true);
            return Result.Ok();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result.Fail(Failed with { Detail = $"0x{ex.HResult:X8}" });
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    private void WriteInformation(ZipArchive archive, VersionReport versions)
    {
        var current = settings.Load();
        using var writer = new StreamWriter(archive.CreateEntry("bilgi.txt").Open());
        writer.WriteLine(Invariant($"Tarih (UTC): {(time ?? TimeProvider.System).GetUtcNow():yyyy-MM-dd HH:mm:ss}"));
        writer.WriteLine($"AWL: {versions.Awl}");
        writer.WriteLine($"Claude Code: {versions.Claude ?? "-"}");
        writer.WriteLine($"Python: {versions.Python ?? "-"}");
        writer.WriteLine($"Windows: {versions.Windows} ({versions.Architecture})");
        writer.WriteLine(Invariant($"Saklama süresi (gün): {current.RetentionDays}"));
        writer.WriteLine($"Terminal: {current.TerminalPreference}");
        writer.WriteLine();
        writer.WriteLine("Kurulum adımları:");
        foreach (var step in current.SetupSteps.OrderBy(step => step.Key, StringComparer.Ordinal))
        {
            writer.WriteLine($"  {step.Key}: {step.Value}");
        }
    }

    private IEnumerable<string> LogFiles()
    {
        if (!Directory.Exists(paths.LogsDirectory))
        {
            return [];
        }
        // Only AWL's own text logs; archives such as setup-diagnostics.zip and any other file stay out.
        return Directory.EnumerateFiles(paths.LogsDirectory, "*.log", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal);
    }

    private static void CopyLog(ZipArchive archive, string log)
    {
        // Serilog keeps the current file open for writing; share it instead of failing.
        using var source = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (source.Length > MaxLogBytes)
        {
            source.Seek(-MaxLogBytes, SeekOrigin.End);
        }
        using var entry = archive.CreateEntry($"gunlukler/{Path.GetFileName(log)}", CompressionLevel.Optimal).Open();
        source.CopyTo(entry);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover temporary file is harmless and hidden by its name.
        }
    }

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
