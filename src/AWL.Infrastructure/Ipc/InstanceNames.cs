using System.Security.Principal;

namespace AWL.Infrastructure.Ipc;

/// <summary>Per-user names of the single-instance lock and pipe (Local\AWL-&lt;SID&gt;, AWL-&lt;SID&gt;).</summary>
public static class InstanceNames
{
    private static readonly Lazy<string> UserKey = new(() => OperatingSystem.IsWindows()
        ? WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("The user SID is unavailable.")
        : Environment.UserName);

    public static string Mutex => $@"Local\AWL-{UserKey.Value}";

    public static string Pipe => $"AWL-{UserKey.Value}";
}
