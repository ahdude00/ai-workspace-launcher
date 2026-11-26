using AWL.Core.Categories;
using AWL.Core.Claude;
using AWL.Core.Common;
using AWL.Core.Configuration;
using AWL.Core.Sessions;
using AWL.Core.Terminal;
using AWL.Desktop.Services;
using NSubstitute;

namespace AWL.Desktop.Tests;

/// <summary>A real <see cref="SessionService"/> over in-memory and substitute collaborators.</summary>
internal sealed class SessionFixture : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("awl-desktop-");

    public SessionFixture()
    {
        var paths = new AwlPaths(_root.FullName, _root.FullName, _root.FullName);
        Clock.UtcNow.Returns(_ => Now);
        Monitor.RunningSessionIds.Returns(Running);
        Installation.ExecutablePath.Returns((string?)null);
        Installation.Version.Returns("2.1.4");
        Terminal.LaunchAsync(Arg.Any<ClaudeLaunchSpec>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Launches.Add(call.Arg<ClaudeLaunchSpec>());
                return Result.Ok();
            });
        Service = new SessionService(Repository, new ClaudeCommandBuilder(paths, Installation), Terminal, Monitor,
            History, Installation, Clock);
    }

    public string Root => _root.FullName;
    public DateTimeOffset Now { get; set; } = new(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);
    public InMemorySessionRepository Repository { get; } = new();
    public InMemoryCategoryRepository Categories { get; } = new();
    public ITerminalLauncher Terminal { get; } = Substitute.For<ITerminalLauncher>();
    public IClaudeProcessMonitor Monitor { get; } = Substitute.For<IClaudeProcessMonitor>();
    public IClaudeHistoryStore History { get; } = Substitute.For<IClaudeHistoryStore>();
    public IClaudeInstallation Installation { get; } = Substitute.For<IClaudeInstallation>();
    public IClock Clock { get; } = Substitute.For<IClock>();
    public HashSet<Guid> Running { get; } = [];
    public List<ClaudeLaunchSpec> Launches { get; } = [];
    public SessionService Service { get; }

    public async Task<Session> AddAsync(string title, Guid? category = null, bool archived = false,
        DateTimeOffset? lastActivity = null, bool opened = false, string? folder = null)
    {
        var session = new Session(Guid.NewGuid(), title, category, folder ?? Root, Guid.NewGuid(), null, archived,
            Now.AddDays(-10), opened ? Now.AddDays(-1) : null, lastActivity);
        await Repository.AddAsync(session);
        return session;
    }

    public void Dispose()
    {
        Service.Dispose();
        _root.Delete(recursive: true);
    }
}

internal sealed class InMemorySessionRepository : ISessionRepository
{
    private readonly Dictionary<Guid, Session> _items = [];

    public IReadOnlyCollection<Session> Items => [.. _items.Values];

    public Task<Session?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.GetValueOrDefault(id));

    public Task<IReadOnlyList<Session>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Session>>([.. _items.Values]);

    public Task AddAsync(Session session, CancellationToken cancellationToken = default)
    {
        _items.Add(session.Id, session);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(Session session, CancellationToken cancellationToken = default)
    {
        if (!_items.ContainsKey(session.Id))
        {
            return Task.FromResult(false);
        }
        _items[session.Id] = session;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.Remove(id));
}

internal sealed class InMemoryCategoryRepository : ICategoryRepository
{
    private readonly Dictionary<Guid, Category> _items = [];

    public IReadOnlyCollection<Category> Items => [.. _items.Values];

    public Task<Category?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.GetValueOrDefault(id));

    public Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Category>>([.. _items.Values.OrderBy(c => c.SortOrder).ThenBy(c => c.Name)]);

    public Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        _items.Add(category.Id, category);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(Category category, CancellationToken cancellationToken = default)
    {
        if (!_items.ContainsKey(category.Id))
        {
            return Task.FromResult(false);
        }
        _items[category.Id] = category;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.Remove(id));
}

/// <summary>Collects posted work so that tests decide when the "UI thread" runs it.</summary>
internal sealed class ManualDispatcher : IUiDispatcher
{
    private readonly Queue<Func<Task>> _queue = new();

    public int Pending => _queue.Count;

    public void Post(Func<Task> work) => _queue.Enqueue(work);

    public async Task RunAllAsync()
    {
        while (_queue.TryDequeue(out var work))
        {
            await work();
        }
    }
}
