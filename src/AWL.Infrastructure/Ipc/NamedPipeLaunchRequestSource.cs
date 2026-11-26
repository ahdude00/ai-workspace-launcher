using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using AWL.Core.Shell;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AWL.Infrastructure.Ipc;

/// <summary>
/// Receives launch requests from later AWL processes over a named pipe that only the current user can open
/// (SAG-05, GUV-09). Every message is parsed defensively and its path is validated again in this process.
/// </summary>
public sealed partial class NamedPipeLaunchRequestSource(ILogger<NamedPipeLaunchRequestSource> logger)
    : BackgroundService, ILaunchRequestSource
{
    /// <summary>A launch message is one short JSON line; anything longer is not from AWL.</summary>
    internal const int MaxMessageBytes = 64 * 1024;

    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);

    public event EventHandler<LaunchRequest>? RequestReceived;

    /// <summary>The pipe to listen on; tests use a unique name.</summary>
    internal string PipeName { get; init; } = InstanceNames.Pipe;

    /// <summary>
    /// Sends a request to the running AWL. Returns true once that process has answered, whatever it decided;
    /// false when nothing answered within <paramref name="timeout"/>.
    /// </summary>
    public static async Task<bool> TrySendAsync(LaunchRequest request, TimeSpan timeout, string? pipeName = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var cancel = new CancellationTokenSource(timeout);
        byte[] message = Encode(request);
        // The listener serves one client at a time and briefly has no pipe between two clients;
        // a connection that fails in that gap is tried again until the timeout.
        while (true)
        {
            try
            {
                // CurrentUserOnly also verifies that the server belongs to this user.
                await using var client = new NamedPipeClientStream(".", pipeName ?? InstanceNames.Pipe, PipeDirection.InOut,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await client.ConnectAsync(cancel.Token).ConfigureAwait(false);
                await client.WriteAsync(message, cancel.Token).ConfigureAwait(false);
                await client.FlushAsync(cancel.Token).ConfigureAwait(false);
                var answer = new byte[1];
                if (await client.ReadAsync(answer, cancel.Token).ConfigureAwait(false) == 1)
                {
                    return true;
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex) when (ex is IOException or TimeoutException)
            {
                // Retried below.
            }
            catch (UnauthorizedAccessException)
            {
                // A pipe of another user (or a squatter) answers this name: never send to it.
                return false;
            }
            try
            {
                await Task.Delay(RetryDelay, cancel.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var server = CreateServer();
                await server.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);
                LaunchRequest? request = await ReadAsync(server, stoppingToken).ConfigureAwait(false);
                await server.WriteAsync(new[] { (byte)(request is null ? 0 : 1) }, stoppingToken).ConfigureAwait(false);
                await server.FlushAsync(stoppingToken).ConfigureAwait(false);
                if (request is null)
                {
                    LogRejected(logger);
                    continue;
                }
                RequestReceived?.Invoke(this, request);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                // A client that disconnects or stalls must not stop the listener.
                LogConnectionFailed(logger, ex.HResult);
                await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private NamedPipeServerStream CreateServer()
    {
        const PipeOptions options = PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance;
        if (!OperatingSystem.IsWindows())
        {
            return new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                options | PipeOptions.CurrentUserOnly);
        }
        // Only the current user may connect; FirstPipeInstance refuses to share a name another process created first.
        var security = new PipeSecurity();
        using var identity = WindowsIdentity.GetCurrent();
        security.AddAccessRule(new PipeAccessRule(identity.User!, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        return NamedPipeServerStreamAcl.Create(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, options,
            0, 0, security);
    }

    private static async Task<LaunchRequest?> ReadAsync(Stream stream, CancellationToken stoppingToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(ReadTimeout);
        var buffer = new byte[MaxMessageBytes + 1];
        int length = 0;
        while (length < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(length), timeout.Token).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
            length += read;
            if (Array.IndexOf(buffer, (byte)'\n', length - read, read) >= 0)
            {
                break;
            }
        }
        return length > MaxMessageBytes ? null : Decode(buffer.AsSpan(0, length));
    }

    internal static byte[] Encode(LaunchRequest request) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Message
        {
            Kind = request.Kind.ToString().ToLowerInvariant(),
            Path = request.Path,
        }) + "\n");

    internal static LaunchRequest? Decode(ReadOnlySpan<byte> bytes)
    {
        int end = bytes.IndexOf((byte)'\n');
        if (end < 0)
        {
            return null;
        }
        try
        {
            var message = JsonSerializer.Deserialize<Message>(bytes[..end]);
            LaunchKind? kind = message?.Kind switch
            {
                "activate" => LaunchKind.Activate,
                "folder" => LaunchKind.Folder,
                "file" => LaunchKind.File,
                _ => null,
            };
            return kind is { } known && LaunchRequest.Create(known, message!.Path) is { IsSuccess: true } valid
                ? valid.Value
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class Message
    {
        [System.Text.Json.Serialization.JsonPropertyName("kind")]
        public string? Kind { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("path")]
        public string? Path { get; init; }
    }

    [LoggerMessage(1700, LogLevel.Warning, "A launch request from another AWL process was rejected.")]
    private static partial void LogRejected(ILogger logger);

    [LoggerMessage(1701, LogLevel.Warning, "A launch request connection failed (HRESULT {HResult:X8}).")]
    private static partial void LogConnectionFailed(ILogger logger, int hResult);
}
