using System.Security.Cryptography;
using AWL.Core.Configuration;

namespace AWL.Infrastructure.Documents;

public static class UdfToolInstaller
{
    private static Stream OpenAsset() => typeof(UdfToolInstaller).Assembly.GetManifestResourceStream("AWL.udf_tool.py")
        ?? throw new InvalidOperationException("Missing bundled UDF tool.");

    public static bool IsCurrent(AwlPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (!File.Exists(paths.UdfToolFile))
        {
            return false;
        }
        using var asset = OpenAsset();
        using var installed = File.Open(paths.UdfToolFile, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        return SHA256.HashData(installed).SequenceEqual(SHA256.HashData(asset));
    }

    public static void EnsureInstalled(AwlPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (IsCurrent(paths))
        {
            return;
        }
        using var asset = OpenAsset();
        Directory.CreateDirectory(paths.ToolsDirectory);
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
