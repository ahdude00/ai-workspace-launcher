using System.IO;
using System.Text;

namespace AWL.Desktop.Terminal;

internal static class TerminalOutput
{
    // One reader retains a partial UTF-8 character between pipe reads. Keep ANSI sequences intact.
    internal static async Task PumpAsync(Stream output, Func<string, Task> send, CancellationToken token)
    {
        using var reader = new StreamReader(output, new UTF8Encoding(false), false, 4096, leaveOpen: true);
        var chars = new char[4096];
        var batch = new StringBuilder();
        Task<int> read = reader.ReadAsync(chars.AsMemory(), token).AsTask();
        while (true)
        {
            int count = await read.ConfigureAwait(false);
            if (count == 0)
            {
                return;
            }
            batch.Append(chars, 0, count);
            read = reader.ReadAsync(chars.AsMemory(), token).AsTask();
            await Task.Delay(16, token).ConfigureAwait(false);
            while (read.IsCompletedSuccessfully && read.Result > 0 && batch.Length < 32768)
            {
                batch.Append(chars, 0, read.Result);
                read = reader.ReadAsync(chars.AsMemory(), token).AsTask();
            }
            await send(batch.ToString()).ConfigureAwait(false);
            batch.Clear();
        }
    }
}
