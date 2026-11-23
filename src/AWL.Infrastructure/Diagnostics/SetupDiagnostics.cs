using System.IO.Compression;
using AWL.Core.Configuration;

namespace AWL.Infrastructure.Diagnostics;

/// <summary>Exports only the current setup attempt. Document contents and environment values are excluded.</summary>
public static class SetupDiagnostics
{
    public static string Save(AwlPaths paths, string version, IEnumerable<string> summary)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(summary);
        Directory.CreateDirectory(paths.LogsDirectory);
        string target = Path.Combine(paths.LogsDirectory, "setup-diagnostics.zip");
        string temporary = Path.Combine(paths.LogsDirectory, Path.GetRandomFileName());
        using var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        try
        {
            using (file)
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry("kurulum.txt").Open()))
            {
                writer.WriteLine($"AWL {version}");
                writer.WriteLine(Environment.OSVersion.VersionString);
                foreach (string line in summary)
                {
                    writer.WriteLine(line);
                }
            }
            File.Move(temporary, target, overwrite: true);
            return target;
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
