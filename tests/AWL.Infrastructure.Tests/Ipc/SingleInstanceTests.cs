using System.IO.Pipes;
using System.Text;
using AWL.Core.Shell;
using AWL.Infrastructure.Ipc;
using Microsoft.Extensions.Logging.Abstractions;

namespace AWL.Infrastructure.Tests.Ipc;

public sealed class SingleInstanceTests : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("awl-ipc-");
    private readonly string _pipe = "AWL-test-" + Guid.NewGuid().ToString("N");
    private readonly NamedPipeLaunchRequestSource _server;
    private readonly List<LaunchRequest> _received = [];
    private readonly SemaphoreSlim _signal = new(0);

    public SingleInstanceTests()
    {
        _server = new NamedPipeLaunchRequestSource(NullLogger<NamedPipeLaunchRequestSource>.Instance) { PipeName = _pipe };
        _server.RequestReceived += (_, request) =>
        {
            lock (_received)
            {
                _received.Add(request);
            }
            _signal.Release();
        };
    }

    [Fact]
    public void Only_one_lock_holder_per_name()
    {
        string name = @"Local\AWL-test-" + Guid.NewGuid().ToString("N");
        using (var first = SingleInstanceLock.TryAcquire(name))
        {
            first.ShouldNotBeNull();
            // A separate thread stands in for a second AWL process (a mutex is owned per thread).
            OnOtherThread(name).ShouldBeFalse();
        }
        OnOtherThread(name).ShouldBeTrue();
    }

    // Acquires and releases on one thread, as the lock requires, and reports whether it was free.
    private static bool OnOtherThread(string name)
    {
        bool acquired = false;
        var thread = new Thread(() =>
        {
            using var lockHandle = SingleInstanceLock.TryAcquire(name);
            acquired = lockHandle is not null;
        });
        thread.Start();
        thread.Join();
        return acquired;
    }

    [Fact]
    public async Task Requests_reach_the_running_instance_and_are_validated_again()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root.FullName, "İş Dosyaları; 2026"));
        string file = Path.Combine(folder.FullName, "Dilekçe ş.udf");
        await File.WriteAllTextAsync(file, "x");
        await _server.StartAsync(default);

        (await NamedPipeLaunchRequestSource.TrySendAsync(LaunchRequest.Activate, Timeout, _pipe)).ShouldBeTrue();
        (await NamedPipeLaunchRequestSource.TrySendAsync(
            LaunchRequest.Create(LaunchKind.Folder, folder.FullName).Value, Timeout, _pipe)).ShouldBeTrue();
        (await NamedPipeLaunchRequestSource.TrySendAsync(
            LaunchRequest.Create(LaunchKind.File, file).Value, Timeout, _pipe)).ShouldBeTrue();
        for (int i = 0; i < 3; i++)
        {
            (await _signal.WaitAsync(Timeout)).ShouldBeTrue();
        }

        _received.Select(r => (r.Kind, r.Path)).ShouldBe(
        [
            (LaunchKind.Activate, null),
            (LaunchKind.Folder, folder.FullName),
            (LaunchKind.File, file),
        ]);
    }

    [Theory]
    [InlineData("not json\n")]
    [InlineData("{\"kind\":\"folder\",\"path\":\"relative\"}\n")]
    [InlineData("{\"kind\":\"folder\",\"path\":\"\\\\\\\\.\\\\PhysicalDrive0\"}\n")]
    [InlineData("{\"kind\":\"run\",\"path\":null}\n")]
    [InlineData("{\"kind\":\"activate\",\"path\":\"/tmp\"}\n")]
    [InlineData("{\"kind\":\"folder\"}\n")]
    public async Task Malformed_or_unsafe_messages_are_answered_but_ignored(string message)
    {
        await _server.StartAsync(default);

        (await SendRawAsync(Encoding.UTF8.GetBytes(message))).ShouldBe(0);
        (await NamedPipeLaunchRequestSource.TrySendAsync(LaunchRequest.Activate, Timeout, _pipe)).ShouldBeTrue();
        (await _signal.WaitAsync(Timeout)).ShouldBeTrue();

        _received.ShouldHaveSingleItem().Kind.ShouldBe(LaunchKind.Activate);
    }

    [Fact]
    public async Task Oversized_messages_are_rejected()
    {
        await _server.StartAsync(default);
        var huge = new byte[NamedPipeLaunchRequestSource.MaxMessageBytes + 10];
        Array.Fill(huge, (byte)'a');

        (await SendRawAsync(huge)).ShouldBe(0);
        _received.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_stalled_client_is_dropped_and_the_listener_continues()
    {
        await _server.StartAsync(default);

        // No line end and the connection stays open: the server stops waiting after its read timeout.
        (await SendRawAsync(Encoding.UTF8.GetBytes("{\"kind\":\"activate\""))).ShouldBe(-1);
        (await NamedPipeLaunchRequestSource.TrySendAsync(LaunchRequest.Activate, Timeout, _pipe)).ShouldBeTrue();
        (await _signal.WaitAsync(Timeout)).ShouldBeTrue();
    }

    [Fact]
    public async Task Sending_without_a_running_instance_fails_quickly()
    {
        (await NamedPipeLaunchRequestSource.TrySendAsync(LaunchRequest.Activate, TimeSpan.FromMilliseconds(300), _pipe))
            .ShouldBeFalse();
    }

    private async Task<int> SendRawAsync(byte[] bytes)
    {
        await using var client = new NamedPipeClientStream(".", _pipe, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var cancel = new CancellationTokenSource(Timeout);
        await client.ConnectAsync(cancel.Token);
        await client.WriteAsync(bytes, cancel.Token);
        await client.FlushAsync(cancel.Token);
        var answer = new byte[1];
        return await client.ReadAsync(answer, cancel.Token) == 1 ? answer[0] : -1;
    }

    public async ValueTask DisposeAsync()
    {
        await _server.StopAsync(default);
        _server.Dispose();
        _signal.Dispose();
        _root.Delete(recursive: true);
    }
}
