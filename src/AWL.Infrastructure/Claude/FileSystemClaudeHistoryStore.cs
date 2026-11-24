using AWL.Core.Claude;
using AWL.Core.Common;
using AWL.Core.Configuration;

namespace AWL.Infrastructure.Claude;

internal sealed class FileSystemClaudeHistoryStore(AwlPaths paths, ClaudeHistoryOptions options, IClock clock)
    : IClaudeHistoryStore
{
    private readonly Lock _gate = new();
    private DateTimeOffset _expires;
    private Dictionary<Guid, HistoryInfo> _index = [];

    public HistoryInfo? Find(Guid claudeSessionId)
    {
        lock (_gate)
        {
            if (clock.UtcNow >= _expires)
            {
                var next = new Dictionary<Guid, HistoryInfo>();
                foreach (var project in ProjectDirectories())
                {
                    foreach (var file in project.EnumerateFiles("*.jsonl", SafeChildren))
                    {
                        if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(file.Name), "D", out var id)
                            && (!next.TryGetValue(id, out var previous) || file.LastWriteTimeUtc > previous.LastModifiedUtc))
                        {
                            next[id] = new HistoryInfo(file.LastWriteTimeUtc);
                        }
                    }
                }
                _index = next;
                _expires = clock.UtcNow.AddSeconds(2);
            }
            return _index.GetValueOrDefault(claudeSessionId);
        }
    }

    private static readonly EnumerationOptions SafeChildren = new()
    {
        AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false,
    };

    private DirectoryInfo[] ProjectDirectories()
    {
        if (!AccessibleDirectory(paths.ClaudeConfigDirectory) || !AccessibleDirectory(paths.ClaudeProjectsDirectory))
        {
            return [];
        }
        return new DirectoryInfo(paths.ClaudeProjectsDirectory).GetDirectories("*", SafeChildren);
    }

    private static bool AccessibleDirectory(string path)
    {
        var attributes = Attributes(path);
        if (attributes is null)
        {
            return false;
        }
        if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) == 0)
        {
            throw new IOException($"Unexpected directory type: {path}");
        }
        return true;
    }

    private static FileAttributes? Attributes(string path)
    {
        try { return File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public Task<DeleteReport> DeleteAsync(Guid claudeSessionId, CancellationToken cancellationToken) =>
        Task.Run(() => Delete(claudeSessionId, cancellationToken), cancellationToken);

    private DeleteReport Delete(Guid id, CancellationToken token)
    {
        var failures = new List<DeleteFailure>();
        string name = id.ToString("D");
        try
        {
            Attempt(paths.ClaudeProjectsDirectory, () =>
            {
                foreach (var project in ProjectDirectories())
                {
                    Remove(Path.Combine(project.FullName, name + ".jsonl"), directoryExpected: false);
                    Remove(Path.Combine(project.FullName, name), directoryExpected: true);
                }
            });
            Remove(Path.Combine(paths.ClaudeDebugDirectory, name + ".txt"), directoryExpected: false);
            foreach (string template in options.ExtraSessionPaths)
            {
                Attempt(template, () =>
                {
                    var parts = template.Split(['/', '\\']);
                    if (Path.IsPathRooted(template) || parts.Any(part => part.Length == 0 || part.EndsWith('.') || part.EndsWith(' ')
                        || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || part.Contains(':'))
                        || !parts.Any(part => part == "{uuid}" || Path.GetFileNameWithoutExtension(part) == "{uuid}"))
                    {
                        throw new ArgumentException("Expected a relative session-specific path containing {uuid}.");
                    }
                    Remove(Path.Combine(paths.ClaudeConfigDirectory,
                        Path.Combine(parts).Replace("{uuid}", name, StringComparison.Ordinal)));
                });
            }
            return new DeleteReport(failures);
        }
        finally
        {
            lock (_gate) { _expires = DateTimeOffset.MinValue; }
        }

        void Attempt(string path, Action action)
        {
            token.ThrowIfCancellationRequested();
            try { action(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                failures.Add(new DeleteFailure(path, ex.Message));
            }
        }

        void Remove(string path, bool? directoryExpected = null) => Attempt(path, () =>
        {
            var parent = new DirectoryInfo(Path.GetDirectoryName(path)!);
            while (!string.Equals(Path.TrimEndingDirectorySeparator(parent.FullName), Path.TrimEndingDirectorySeparator(paths.ClaudeConfigDirectory), StringComparison.OrdinalIgnoreCase))
            {
                if (!AccessibleDirectory(parent.FullName)) { return; }
                parent = parent.Parent ?? throw new IOException("Path outside Claude configuration.");
            }
            if (!AccessibleDirectory(parent.FullName)) { return; }
            var attributes = Attributes(path);
            if (attributes is null) { return; }
            bool directory = (attributes & FileAttributes.Directory) != 0;
            if (directoryExpected.HasValue && directoryExpected != directory)
            {
                throw new IOException("Unexpected session entry type.");
            }
            if (directory)
            {
                // Directory.Delete removes junctions themselves and does not recurse into reparse points.
                Directory.Delete(path, recursive: (attributes & FileAttributes.ReparsePoint) == 0);
            }
            else
            {
                File.Delete(path);
            }
        });
    }
}
