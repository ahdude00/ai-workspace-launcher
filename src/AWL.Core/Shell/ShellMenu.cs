namespace AWL.Core.Shell;

/// <summary>One "AWL ile aç" entry: the key below HKCU\Software\Classes and the command it runs.</summary>
public sealed record ShellMenuEntry(string ClassKey, string Command)
{
    /// <summary>The verb key AWL owns below <see cref="ClassKey"/>; removal deletes only this key.</summary>
    public string VerbKey => $@"{ClassKey}\shell\{ShellMenu.VerbName}";
}

/// <summary>The Explorer context menu entries (SAG-01…03, D09). Pure; registry access lives in Infrastructure.</summary>
public static class ShellMenu
{
    public const string VerbName = "AWL";

    /// <summary>File types offered "AWL ile aç" (SAG-03).</summary>
    public static readonly IReadOnlyList<string> FileExtensions = [".pdf", ".docx", ".xlsx", ".udf", ".png", ".jpg"];

    /// <summary>
    /// Builds every entry for the given AWL.exe. Paths are always quoted so that spaces, Turkish letters and
    /// semicolons reach <see cref="LaunchArgumentParser"/> as one argument (SAG-06).
    /// </summary>
    public static IReadOnlyList<ShellMenuEntry> Entries(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (!Path.IsPathFullyQualified(executablePath) || executablePath.Contains('"', StringComparison.Ordinal))
        {
            throw new ArgumentException("A fully qualified executable path without quotes is required.", nameof(executablePath));
        }
        string exe = $"\"{executablePath}\"";
        return
        [
            new(@"Directory", $"{exe} {LaunchArgumentParser.FolderFlag} \"%1\""),
            new(@"Directory\Background", $"{exe} {LaunchArgumentParser.FolderFlag} \"%V\""),
            .. FileExtensions.Select(extension => new ShellMenuEntry(
                $@"SystemFileAssociations\{extension}", $"{exe} {LaunchArgumentParser.FileFlag} \"%1\"")),
        ];
    }

    /// <summary>The icon value: AWL.exe's first icon.</summary>
    public static string Icon(string executablePath) => $"\"{executablePath}\",0";
}

/// <summary>Registers the Explorer context menu for the current user only (HKCU; no UAC prompt).</summary>
public interface IShellIntegration
{
    /// <summary>Whether every entry exists with exactly these values.</summary>
    bool IsRegistered(string executablePath, string menuText);

    void Register(string executablePath, string menuText);

    /// <summary>Removes AWL's verb keys and nothing else (SAG-07).</summary>
    void Unregister();
}
