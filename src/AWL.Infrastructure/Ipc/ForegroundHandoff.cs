using System.Runtime.InteropServices;

namespace AWL.Infrastructure.Ipc;

/// <summary>
/// Lets the running AWL take the foreground when a later AWL process hands it a request. Windows only gives the
/// foreground to the process that received the last input, which is the short-lived second process here.
/// </summary>
public static partial class ForegroundHandoff
{
    private const int AnyProcess = -1;

    public static void AllowAny()
    {
        if (OperatingSystem.IsWindows())
        {
            _ = AllowSetForegroundWindow(AnyProcess);
        }
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(int processId);
}
