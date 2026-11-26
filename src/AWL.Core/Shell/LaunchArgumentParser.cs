using AWL.Core.Common;

namespace AWL.Core.Shell;

/// <summary>
/// Parses AWL.exe's command line (SAG-04): no arguments, <c>--folder "&lt;path&gt;"</c> or <c>--file "&lt;path&gt;"</c>.
/// Anything else, including unknown or repeated flags, is rejected.
/// </summary>
public static class LaunchArgumentParser
{
    public const string FolderFlag = "--folder";
    public const string FileFlag = "--file";

    public static Result<LaunchRequest> Parse(IReadOnlyList<string> args, IFileProbe? probe = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Count == 0)
        {
            return Result.Ok(LaunchRequest.Activate);
        }
        if (args.Count != 2)
        {
            return Result.Fail<LaunchRequest>(LaunchRequest.Errors.Invalid);
        }
        LaunchKind? kind = args[0] switch
        {
            FolderFlag => LaunchKind.Folder,
            FileFlag => LaunchKind.File,
            _ => null,
        };
        return kind is { } known
            ? LaunchRequest.Create(known, args[1], probe)
            : Result.Fail<LaunchRequest>(LaunchRequest.Errors.Invalid);
    }
}
