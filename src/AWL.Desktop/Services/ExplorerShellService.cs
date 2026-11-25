using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace AWL.Desktop.Services;

internal sealed class ExplorerShellService : IShellService
{
    public bool OpenFolder(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"))
            {
                UseShellExecute = false,
                ArgumentList = { path },
            };
            using var process = Process.Start(start);
            return process is not null;
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
