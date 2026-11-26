using AWL.Core.Shell;
using Microsoft.Win32;

namespace AWL.Infrastructure.Shell;

/// <summary>Writes AWL's context menu entries below HKCU\Software\Classes (GUV-06: user scope only).</summary>
public sealed class HkcuShellIntegration : IShellIntegration
{
    private const string CommandKey = "command";

    /// <summary>The classes root below HKEY_CURRENT_USER; tests use a private key.</summary>
    internal string ClassesRoot { get; init; } = @"Software\Classes";

    public bool IsRegistered(string executablePath, string menuText)
    {
        foreach (var entry in ShellMenu.Entries(executablePath))
        {
            using var verb = Registry.CurrentUser.OpenSubKey(Path(entry.VerbKey));
            using var command = verb?.OpenSubKey(CommandKey);
            if (verb?.GetValue("MUIVerb") as string != menuText
                || verb.GetValue("Icon") as string != ShellMenu.Icon(executablePath)
                || command?.GetValue(null) as string != entry.Command)
            {
                return false;
            }
        }
        return true;
    }

    public void Register(string executablePath, string menuText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(menuText);
        foreach (var entry in ShellMenu.Entries(executablePath))
        {
            using var verb = Registry.CurrentUser.CreateSubKey(Path(entry.VerbKey), writable: true);
            verb.SetValue("MUIVerb", menuText, RegistryValueKind.String);
            verb.SetValue("Icon", ShellMenu.Icon(executablePath), RegistryValueKind.String);
            using var command = verb.CreateSubKey(CommandKey, writable: true);
            command.SetValue(null, entry.Command, RegistryValueKind.String);
        }
    }

    public void Unregister()
    {
        // The executable path does not matter for the key names.
        foreach (var entry in ShellMenu.Entries(@"C:\AWL.exe"))
        {
            Registry.CurrentUser.DeleteSubKeyTree(Path(entry.VerbKey), throwOnMissingSubKey: false);
        }
    }

    private string Path(string relative) => $@"{ClassesRoot}\{relative}";
}
