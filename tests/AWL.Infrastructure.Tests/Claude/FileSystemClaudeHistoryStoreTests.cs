using AWL.Core.Common;
using AWL.Core.Configuration;
using AWL.Infrastructure.Claude;
using NSubstitute;

namespace AWL.Infrastructure.Tests.Claude;

public sealed class FileSystemClaudeHistoryStoreTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory();
    private readonly AwlPaths _paths;
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly Guid _id = Guid.NewGuid();

    public FileSystemClaudeHistoryStoreTests()
    {
        _paths = new AwlPaths(_root.FullName, _root.FullName, _root.FullName);
        _clock.UtcNow.Returns(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Finds_latest_transcript_and_removes_only_session_data()
    {
        var store = Store("snapshots/{uuid}.log");
        store.Find(_id).ShouldBeNull();
        var first = Write($"projects/one/{_id}.jsonl");
        var second = Write($"projects/two/{_id}.jsonl");
        File.SetLastWriteTimeUtc(first, DateTime.UtcNow.AddDays(-1));
        Write($"projects/two/{_id}/agents/a.jsonl");
        Write($"debug/{_id}.txt");
        Write($"snapshots/{_id}.log");
        var other = Write($"projects/one/{Guid.NewGuid()}.jsonl");
        var unrelated = Write($"docs/{_id}.jsonl");
        store.Find(_id).ShouldBeNull();
        _clock.UtcNow.Returns(DateTimeOffset.UtcNow.AddMinutes(1));
        store.Find(_id)!.LastModifiedUtc.ShouldBe(new DateTimeOffset(File.GetLastWriteTimeUtc(second)));

        (await store.DeleteAsync(_id, default)).IsComplete.ShouldBeTrue();
        (await store.DeleteAsync(_id, default)).IsComplete.ShouldBeTrue();
        store.Find(_id).ShouldBeNull();
        File.Exists(other).ShouldBeTrue();
        File.Exists(unrelated).ShouldBeTrue();
        Directory.GetFileSystemEntries(_paths.ClaudeConfigDirectory, $"*{_id}*", SearchOption.AllDirectories)
            .ShouldBe([unrelated]);
    }

    [Fact]
    public async Task Reports_invalid_paths_and_continues_other_deletions()
    {
        var protectedFile = Write("docs/readme.txt");
        string transcript = Write($"projects/one/{_id}.jsonl");
        var store = Store("../{uuid}", "docs", "docs/readme.txt");
        var report = await store.DeleteAsync(_id, default);
        report.Failures.Count.ShouldBe(3);
        File.Exists(protectedFile).ShouldBeTrue();
        File.Exists(transcript).ShouldBeFalse();
    }

    [Fact]
    public async Task Directory_in_place_of_transcript_is_reported_and_preserved()
    {
        var file = Write($"projects/one/{_id}.jsonl/user-document");
        var log = Write($"debug/{_id}.txt");
        var report = await Store().DeleteAsync(_id, default);
        report.IsComplete.ShouldBeFalse();
        File.Exists(file).ShouldBeTrue();
        File.Exists(log).ShouldBeFalse();
    }

    private FileSystemClaudeHistoryStore Store(params string[] paths) =>
        new(_paths, new ClaudeHistoryOptions { ExtraSessionPaths = paths }, _clock);

    private string Write(string relative)
    {
        string path = Path.GetFullPath(relative, _paths.ClaudeConfigDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "fixture");
        return path;
    }

    public void Dispose() => _root.Delete(recursive: true);
}
