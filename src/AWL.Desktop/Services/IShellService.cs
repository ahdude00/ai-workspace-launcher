namespace AWL.Desktop.Services;

/// <summary>Hands folders to Windows Explorer.</summary>
public interface IShellService
{
    /// <summary>Opens the folder in Explorer and returns whether Explorer could be started.</summary>
    bool OpenFolder(string path);
}
