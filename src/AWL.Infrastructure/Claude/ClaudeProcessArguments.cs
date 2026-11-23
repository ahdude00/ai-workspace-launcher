using System.Runtime.InteropServices;
using AWL.Core.Claude;

namespace AWL.Infrastructure.Claude;

internal static partial class ClaudeProcessArguments
{
    internal static Guid? SessionId(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }
        nint argv = CommandLineToArgvW(commandLine, out int count);
        if (argv == 0)
        {
            return null;
        }
        try
        {
            for (int i = 1; i + 1 < count; i++)
            {
                string? argument = Read(i);
                if (argument == "--")
                {
                    break;
                }
                if (argument is ClaudeCli.SessionId or ClaudeCli.Resume && Guid.TryParseExact(Read(i + 1), "D", out var id))
                {
                    return id;
                }
            }
            return null;

            string? Read(int index) => Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, index * IntPtr.Size));
        }
        finally
        {
            LocalFree(argv);
        }
    }

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CommandLineToArgvW(string commandLine, out int count);

    [LibraryImport("kernel32.dll")]
    private static partial nint LocalFree(nint memory);
}
