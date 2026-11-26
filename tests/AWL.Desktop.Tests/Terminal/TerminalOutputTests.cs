using System.Text;
using AWL.Desktop.Terminal;

namespace AWL.Desktop.Tests.Terminal;

public sealed class TerminalOutputTests
{
    [Fact]
    public async Task Split_utf8_and_ansi_sequences_survive_batching()
    {
        const string expected = "\u001b[31mİş görüşü — çığ🌍\u001b[0m";
        using var pipe = new FragmentedStream(Encoding.UTF8.GetBytes(expected));
        var received = new StringBuilder();
        await TerminalOutput.PumpAsync(pipe, chunk =>
        {
            received.Append(chunk);
            return Task.CompletedTask;
        }, CancellationToken.None);
        received.ToString().ShouldBe(expected);
    }

    private sealed class FragmentedStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..1], cancellationToken);
    }
}
