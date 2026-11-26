using AWL.Core.Common;

namespace AWL.Core.Shell;

/// <summary>What a start of AWL.exe asks the running AWL to do.</summary>
public enum LaunchKind
{
    /// <summary>Plain start: only bring the existing window forward.</summary>
    Activate,

    /// <summary><c>--folder "&lt;path&gt;"</c>: a new session in this folder.</summary>
    Folder,

    /// <summary><c>--file "&lt;path&gt;"</c>: a new session in the file's folder; the file path goes to the clipboard.</summary>
    File,
}

/// <summary>A validated request from the command line or from a second AWL process (SAG-04, GUV-09).</summary>
public sealed record LaunchRequest
{
    /// <summary>Longest path accepted; longer input is not a path a user picked in Explorer.</summary>
    public const int MaxPathLength = 32_000;

    public static readonly LaunchRequest Activate = new(LaunchKind.Activate, null);

    private LaunchRequest(LaunchKind kind, string? path)
    {
        Kind = kind;
        Path = path;
    }

    public LaunchKind Kind { get; }

    /// <summary>The full path of the folder or file; null for <see cref="LaunchKind.Activate"/>.</summary>
    public string? Path { get; }

    /// <summary>The folder for the new session: the folder itself or the folder that contains the file.</summary>
    public string? Folder => Kind == LaunchKind.File ? System.IO.Path.GetDirectoryName(Path) : Path;

    /// <summary>
    /// Validates untrusted input. The path must be fully qualified, must not be a device path (\\.\ or \\?\)
    /// and must exist as the expected kind of entry. The same check runs again in the receiving process.
    /// </summary>
    public static Result<LaunchRequest> Create(LaunchKind kind, string? path, IFileProbe? probe = null)
    {
        if (kind == LaunchKind.Activate)
        {
            return path is null ? Result.Ok(Activate) : Result.Fail<LaunchRequest>(Errors.Invalid);
        }
        if (!Enum.IsDefined(kind) || string.IsNullOrWhiteSpace(path) || path.Length > MaxPathLength
            || path.Contains('\0', StringComparison.Ordinal) || IsDevicePath(path)
            || !System.IO.Path.IsPathFullyQualified(path))
        {
            return Result.Fail<LaunchRequest>(Errors.Invalid);
        }

        string full;
        try
        {
            full = System.IO.Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Result.Fail<LaunchRequest>(Errors.Invalid);
        }
        if (IsDevicePath(full))
        {
            return Result.Fail<LaunchRequest>(Errors.Invalid);
        }

        probe ??= FileProbe.Instance;
        bool exists = kind == LaunchKind.Folder ? probe.DirectoryExists(full) : probe.FileExists(full);
        return exists ? Result.Ok(new LaunchRequest(kind, full)) : Result.Fail<LaunchRequest>(Errors.Missing);
    }

    private static bool IsDevicePath(string path) =>
        path.StartsWith(@"\\.\", StringComparison.Ordinal) || path.StartsWith(@"\\?\", StringComparison.Ordinal)
        || path.StartsWith("//./", StringComparison.Ordinal) || path.StartsWith("//?/", StringComparison.Ordinal)
        || path.StartsWith(@"\??\", StringComparison.Ordinal);

    /// <summary>Error codes for launch requests; each code is also its resource key.</summary>
    public static class Errors
    {
        public static readonly AppError Invalid = new("LaunchArgumentsInvalid", "LaunchArgumentsInvalid");
        public static readonly AppError Missing = new("LaunchPathMissing", "LaunchPathMissing");
    }
}

/// <summary>Checks for files and folders; replaceable in tests.</summary>
public interface IFileProbe
{
    bool DirectoryExists(string path);

    bool FileExists(string path);
}

/// <summary>The real file system.</summary>
public sealed class FileProbe : IFileProbe
{
    public static readonly FileProbe Instance = new();

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);
}
