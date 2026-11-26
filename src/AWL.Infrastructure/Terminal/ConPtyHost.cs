using System.Collections;
using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using AWL.Core.Terminal;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Console;
using Windows.Win32.System.Threading;

namespace AWL.Infrastructure.Terminal;

/// <summary>One interactive Windows pseudoconsole. Read output independently of writing input.</summary>
[SupportedOSPlatform("windows10.0.17763")]
public sealed class ConPtyHost : IDisposable
{
    private readonly object _gate = new();
    private HPCON _console;
    private readonly AnonymousPipeServerStream _input;
    private readonly AnonymousPipeServerStream _output;

    private ConPtyHost(HPCON console, AnonymousPipeServerStream input, AnonymousPipeServerStream output, uint processId)
    {
        _console = console;
        _input = input;
        _output = output;
        ProcessId = processId;
    }

    public uint ProcessId { get; }
    public Stream Input => _input;
    public Stream Output => _output;

    public static unsafe ConPtyHost Start(ClaudeLaunchSpec spec, short columns, short rows,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentException.ThrowIfNullOrWhiteSpace(spec.Executable);
        var size = Dimensions(columns, rows);
        var command = new StringBuilder();
        AppendArgument(command, spec.Executable);
        foreach (string argument in spec.Arguments)
        {
            command.Append(' ');
            AppendArgument(command, argument);
        }
        string? block = EnvironmentBlock(environment);
        var input = new AnonymousPipeServerStream(PipeDirection.Out);
        AnonymousPipeServerStream? output = null;
        HPCON console = default;
        try
        {
            output = new AnonymousPipeServerStream(PipeDirection.In);
            PInvoke.CreatePseudoConsole(size, (HANDLE)input.ClientSafePipeHandle.DangerousGetHandle(),
                (HANDLE)output.ClientSafePipeHandle.DangerousGetHandle(), 0, &console).ThrowOnFailure();
            input.DisposeLocalCopyOfClientHandle();
            output.DisposeLocalCopyOfClientHandle();
            uint processId = Spawn(spec, command.Append('\0').ToString().ToCharArray(), block, console);
            return new ConPtyHost(console, input, output, processId);
        }
        catch
        {
            output?.Dispose();
            input.Dispose();
            if (console != default)
            {
                PInvoke.ClosePseudoConsole(console);
            }
            throw;
        }
    }

    private static unsafe uint Spawn(ClaudeLaunchSpec spec, char[] command, string? environment, HPCON console)
    {
        nuint bytes = 0;
        PInvoke.InitializeProcThreadAttributeList(default, 1, 0, &bytes);
        if (bytes == 0)
        {
            throw new Win32Exception();
        }
        var list = (LPPROC_THREAD_ATTRIBUTE_LIST)NativeMemory.Alloc(bytes);
        try
        {
            if (!PInvoke.InitializeProcThreadAttributeList(list, 1, 0, &bytes))
            {
                throw new Win32Exception();
            }
            try
            {
                if (!PInvoke.UpdateProcThreadAttribute(list, 0, PInvoke.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    (void*)console.Value, (nuint)sizeof(nint), null, null))
                {
                    throw new Win32Exception();
                }
                STARTUPINFOEXW startup = default;
                startup.StartupInfo.cb = (uint)sizeof(STARTUPINFOEXW);
                startup.lpAttributeList = list;
                PROCESS_INFORMATION process;
                fixed (char* executable = spec.Executable, arguments = command, cwd = spec.WorkingDirectory, env = environment)
                {
                    if (!PInvoke.CreateProcess(executable, arguments, null, null, false,
                        PROCESS_CREATION_FLAGS.EXTENDED_STARTUPINFO_PRESENT | PROCESS_CREATION_FLAGS.CREATE_UNICODE_ENVIRONMENT,
                        env, cwd, &startup.StartupInfo, &process))
                    {
                        throw new Win32Exception();
                    }
                }
                PInvoke.CloseHandle(process.hThread);
                PInvoke.CloseHandle(process.hProcess);
                return process.dwProcessId;
            }
            finally
            {
                PInvoke.DeleteProcThreadAttributeList(list);
            }
        }
        finally
        {
            NativeMemory.Free(list.Value);
        }
    }

    private static string? EnvironmentBlock(IReadOnlyDictionary<string, string>? overrides)
    {
        if (overrides is null || overrides.Count == 0)
        {
            return null;
        }
        var entries = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry item in Environment.GetEnvironmentVariables())
        {
            entries[(string)item.Key] = (string)item.Value!;
        }
        foreach (var (key, value) in overrides)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);
            if (key.Contains('=') || key.Contains('\0') || value.Contains('\0'))
            {
                throw new ArgumentException("Invalid child environment variable.", nameof(overrides));
            }
            entries[key] = value;
        }
        return string.Join('\0', entries.Select(pair => $"{pair.Key}={pair.Value}")) + "\0";
    }

    private static void AppendArgument(StringBuilder command, string value)
    {
        if (value.Contains('\0'))
        {
            throw new ArgumentException("Argument contains a null character.", nameof(value));
        }
        command.Append('"');
        int slashes = 0;
        foreach (char ch in value)
        {
            if (ch == '\\')
            {
                slashes++;
                continue;
            }
            command.Append('\\', ch == '"' ? slashes * 2 + 1 : slashes).Append(ch);
            slashes = 0;
        }
        command.Append('\\', slashes * 2).Append('"');
    }

    private static COORD Dimensions(short columns, short rows)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        return new COORD { X = columns, Y = rows };
    }

    public void Resize(short columns, short rows)
    {
        var size = Dimensions(columns, rows);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_console == default, this);
            PInvoke.ResizePseudoConsole(_console, size).ThrowOnFailure();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_console == default)
            {
                return;
            }
            _output.Dispose();
            PInvoke.ClosePseudoConsole(_console);
            _input.Dispose();
            _console = default;
        }
    }
}
