using AWL.Core.Common;

namespace AWL.Core.Shell;

/// <summary>
/// Turns files and folders dropped on AWL into text for Claude's prompt (BLG-07, D08): every path passes the same
/// validation as command-line and pipe input and is quoted; the paths are separated by spaces.
/// </summary>
public static class DroppedPaths
{
    /// <summary>The number of paths accepted at once; a drop of more is almost certainly a mistake.</summary>
    public const int MaxCount = 100;

    public static Result<DroppedText> Format(IEnumerable<string> paths, IFileProbe? probe = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        probe ??= FileProbe.Instance;
        var accepted = new List<string>();
        int rejected = 0;
        foreach (string path in paths.Take(MaxCount + 1))
        {
            if (accepted.Count + rejected == MaxCount)
            {
                return Result.Fail<DroppedText>(Errors.TooMany);
            }
            var request = LaunchRequest.Create(LaunchKind.File, path, probe);
            if (!request.IsSuccess && request.Error == LaunchRequest.Errors.Missing)
            {
                request = LaunchRequest.Create(LaunchKind.Folder, path, probe);
            }
            if (request.IsSuccess && !request.Value.Path!.Contains('"', StringComparison.Ordinal))
            {
                accepted.Add($"\"{request.Value.Path}\"");
            }
            else
            {
                rejected++;
            }
        }
        return accepted.Count == 0
            ? Result.Fail<DroppedText>(Errors.NothingUsable)
            : Result.Ok(new DroppedText(string.Join(' ', accepted), accepted.Count, rejected));
    }

    /// <summary>Error codes; each code is also its resource key.</summary>
    public static class Errors
    {
        public static readonly AppError NothingUsable = new("DropNothingUsable", "DropNothingUsable");
        public static readonly AppError TooMany = new("DropTooMany", "DropTooMany");
    }
}

/// <summary>The quoted paths and how many dropped items were used or skipped.</summary>
public sealed record DroppedText(string Text, int Count, int Skipped);
