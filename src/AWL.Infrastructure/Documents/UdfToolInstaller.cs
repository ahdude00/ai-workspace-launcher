using System.Security.Cryptography;
using AWL.Core.Configuration;

namespace AWL.Infrastructure.Documents;

public static class UdfToolInstaller
{
    public static void EnsureInstalled(AwlPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        using Stream asset = typeof(UdfToolInstaller).Assembly.GetManifestResourceStream("AWL.udf_tool.py")
            ?? throw new InvalidOperationException("Missing bundled UDF tool.");
        Directory.CreateDirectory(paths.ToolsDirectory);
        if (File.Exists(paths.UdfToolFile))
        {
            using var installed = File.Open(paths.UdfToolFile, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete);
            if (SHA256.HashData(installed).SequenceEqual(SHA256.HashData(asset)))
            {
                return;
            }
            asset.Position = 0;
        }

        string temporary = Path.Combine(paths.ToolsDirectory, Path.GetRandomFileName());
        var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        try
        {
            using (file)
            {
                asset.CopyTo(file);
            }
            File.Move(temporary, paths.UdfToolFile, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
