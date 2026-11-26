using AWL.Core.Claude;
using AWL.Core.Common;
using AWL.Core.Configuration;
using AWL.Core.Sessions;
using AWL.Core.Terminal;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AWL.Core.Tests.Sessions;

public sealed class SessionServiceTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("awl-sessions-");
    private readonly InMemorySessionRepository _repository = new();
    private readonly ITerminalLauncher _terminal = Substitute.For<ITerminalLauncher>();
    private readonly IClaudeProcessMonitor _monitor = Substitute.For<IClaudeProcessMonitor>();
    private readonly IClaudeHistoryStore _history = Substitute.For<IClaudeHistoryStore>();
    private readonly IClaudeInstallation _installation = Substitute.For<IClaudeInstallation>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly HashSet<Guid> _running = [];
    private readonly List<(ClaudeLaunchSpec Spec, string Title)> _launches = [];
    private readonly AwlPaths _paths;
    private readonly SessionService _service;
    private DateTimeOffset _now = new(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);

    public SessionServiceTests()
    {
        _paths = new AwlPaths(_root.FullName, _root.FullName, _root.FullName);
        _clock.UtcNow.Returns(_ => _now);
        _monitor.RunningSessionIds.Returns(_running);
        _installation.Version.Returns("2.1.4");
        _installation.ExecutablePath.Returns((string?)null);
        _terminal.LaunchAsync(Arg.Any<ClaudeLaunchSpec>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _launches.Add((call.Arg<ClaudeLaunchSpec>(), call.Arg<string>()));
                return Result.Ok();
            });
        _service = new SessionService(_repository, new ClaudeCommandBuilder(_paths, _installation), _terminal,
            _monitor, _history, _installation, _clock);
    }

    private string Workspace => _root.FullName;

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_rejects_a_blank_title(string title)
    {
        var result = await _service.CreateAsync(title, Workspace, null);
        result.Error!.Code.ShouldBe("SessionTitleInvalid");
        _repository.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_rejects_an_overlong_title()
    {
        var result = await _service.CreateAsync(new string('a', SessionService.MaxTitleLength + 1), Workspace, null);
        result.Error!.Code.ShouldBe("SessionTitleInvalid");
    }

    [Fact]
    public async Task Create_rejects_relative_and_missing_folders()
    {
        (await _service.CreateAsync("Dava", "göreli klasör", null)).Error!.Code.ShouldBe("WorkspaceInvalid");
        (await _service.CreateAsync("Dava", Path.Combine(Workspace, "yok"), null)).Error!.Code.ShouldBe("WorkspaceMissing");
        _repository.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_stores_a_trimmed_session_with_generated_identifiers_without_launching()
    {
        int changes = 0;
        _service.Changed += (_, _) => changes++;
        var category = Guid.NewGuid();

        var session = (await _service.CreateAsync("  İş Sözleşmesi  ", Workspace, category)).Value;

        session.Title.ShouldBe("İş Sözleşmesi");
        session.CategoryId.ShouldBe(category);
        session.WorkspacePath.ShouldBe(Workspace);
        session.Id.ShouldNotBe(Guid.Empty);
        session.ClaudeSessionId.ShouldNotBe(Guid.Empty);
        session.ClaudeSessionId.ShouldNotBe(session.Id);
        session.CreatedAt.ShouldBe(_now);
        session.LastOpenedAt.ShouldBeNull();
        _repository.Items.ShouldHaveSingleItem().ShouldBe(session);
        _launches.ShouldBeEmpty();
        changes.ShouldBe(1);
        _service.GetState(session).ShouldBe(SessionState.Closed);
    }

    [Fact]
    public async Task Open_starts_a_new_session_with_its_identifier_and_records_version_and_time()
    {
        var session = await CreateAsync();

        (await _service.OpenAsync(session.Id)).Value.ShouldBe(OpenOutcome.Launched);

        var (spec, title) = _launches.ShouldHaveSingleItem();
        title.ShouldBe(session.Title);
        spec.WorkingDirectory.ShouldBe(Workspace);
        spec.Arguments.Take(2).ShouldBe(["--session-id", session.ClaudeSessionId.ToString("D")]);
        spec.Arguments.ShouldContain("--settings");
        spec.Arguments.ShouldContain("--append-system-prompt-file");
        var stored = _repository.Items.Single();
        stored.LastOpenedAt.ShouldBe(_now);
        stored.LastActivityAt.ShouldBe(_now);
        stored.ClaudeVersion.ShouldBe("2.1.4");
    }

    [Fact]
    public async Task Open_resumes_a_session_that_was_opened_before()
    {
        var session = await OpenedSessionAsync(withHistory: true);
        _installation.Version.Returns("2.2.0");

        (await _service.OpenAsync(session.Id)).Value.ShouldBe(OpenOutcome.Launched);

        _launches.Single().Spec.Arguments.Take(2).ShouldBe(["--resume", session.ClaudeSessionId.ToString("D")]);
        _repository.Items.Single().ClaudeVersion.ShouldBe("2.2.0");
    }

    [Fact]
    public async Task Open_keeps_the_previous_version_when_the_installed_version_is_unknown()
    {
        var session = await OpenedSessionAsync(withHistory: true);
        _installation.Version.Returns((string?)null);

        await _service.OpenAsync(session.Id);

        _repository.Items.Single().ClaudeVersion.ShouldBe("1.0.0");
    }

    [Fact]
    public async Task Open_does_not_start_a_second_process_for_a_running_session()
    {
        var session = await OpenedSessionAsync(withHistory: true);
        _running.Add(session.ClaudeSessionId);

        (await _service.OpenAsync(session.Id)).Value.ShouldBe(OpenOutcome.AlreadyOpen);

        _launches.ShouldBeEmpty();
        await _monitor.Received(1).TryBringToFrontAsync(session.ClaudeSessionId, Arg.Any<CancellationToken>());
        _service.GetState(session).ShouldBe(SessionState.Open);
    }

    [Fact]
    public async Task A_launch_counts_as_open_until_the_monitor_can_observe_it()
    {
        var session = await CreateAsync();
        await _service.OpenAsync(session.Id);

        _service.GetState(_repository.Items.Single()).ShouldBe(SessionState.Open);
        (await _service.OpenAsync(session.Id)).Value.ShouldBe(OpenOutcome.AlreadyOpen);
        _launches.Count.ShouldBe(1);

        // The process never appeared (e.g. it exited at once): after the grace period the session can be opened again.
        _now = _now.AddSeconds(30);
        _history.Find(session.ClaudeSessionId).Returns(new HistoryInfo(_now));
        _service.GetState(_repository.Items.Single()).ShouldBe(SessionState.Closed);
        (await _service.OpenAsync(session.Id)).Value.ShouldBe(OpenOutcome.Launched);
        _launches.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Concurrent_open_requests_start_only_one_process()
    {
        var session = await CreateAsync();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => _service.OpenAsync(session.Id)));

        outcomes.Count(result => result.Value == OpenOutcome.Launched).ShouldBe(1);
        _launches.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Open_refuses_a_session_whose_folder_was_removed()
    {
        var folder = Directory.CreateDirectory(Path.Combine(Workspace, "Dosyalar"));
        var session = (await _service.CreateAsync("Dava", folder.FullName, null)).Value;
        folder.Delete();

        (await _service.OpenAsync(session.Id)).Error!.Code.ShouldBe("WorkspaceMissing");
        _launches.ShouldBeEmpty();
    }

    [Fact]
    public async Task Open_reports_missing_history_instead_of_resuming()
    {
        var session = await OpenedSessionAsync(withHistory: false);

        _service.GetState(session).ShouldBe(SessionState.HistoryMissing);
        (await _service.OpenAsync(session.Id)).Error!.Code.ShouldBe("HistoryMissing");
        _launches.ShouldBeEmpty();
    }

    [Fact]
    public async Task Failed_launch_returns_the_terminal_error_and_leaves_the_session_unopened()
    {
        var session = await CreateAsync();
        _terminal.LaunchAsync(Arg.Any<ClaudeLaunchSpec>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail(new AppError("TerminalLaunchFailed", "TerminalLaunchFailed")));

        (await _service.OpenAsync(session.Id)).Error!.Code.ShouldBe("TerminalLaunchFailed");

        _repository.Items.Single().LastOpenedAt.ShouldBeNull();
        _service.GetState(_repository.Items.Single()).ShouldBe(SessionState.Closed);
    }

    [Fact]
    public async Task Unknown_sessions_are_reported()
    {
        (await _service.OpenAsync(Guid.NewGuid())).Error!.Code.ShouldBe("SessionNotFound");
        (await _service.StartOverAsync(Guid.NewGuid())).Error!.Code.ShouldBe("SessionNotFound");
        (await _service.RelocateAsync(Guid.NewGuid(), Workspace)).Error!.Code.ShouldBe("SessionNotFound");
    }

    [Fact]
    public async Task Start_over_gives_a_session_without_history_a_new_Claude_identifier_in_the_same_folder()
    {
        var session = await OpenedSessionAsync(withHistory: false);

        (await _service.StartOverAsync(session.Id)).Value.ShouldBe(OpenOutcome.Launched);

        var stored = _repository.Items.Single();
        stored.Id.ShouldBe(session.Id);
        stored.Title.ShouldBe(session.Title);
        stored.ClaudeSessionId.ShouldNotBe(session.ClaudeSessionId);
        stored.LastOpenedAt.ShouldBe(_now);
        var spec = _launches.Single().Spec;
        spec.WorkingDirectory.ShouldBe(session.WorkspacePath);
        spec.Arguments.Take(2).ShouldBe(["--session-id", stored.ClaudeSessionId.ToString("D")]);
    }

    [Fact]
    public async Task Start_over_refuses_while_the_old_history_still_exists()
    {
        var session = await OpenedSessionAsync(withHistory: true);

        (await _service.StartOverAsync(session.Id)).Error!.Code.ShouldBe("HistoryStillPresent");

        _repository.Items.Single().ClaudeSessionId.ShouldBe(session.ClaudeSessionId);
        _launches.ShouldBeEmpty();
    }

    [Fact]
    public async Task Relocate_points_the_session_to_an_existing_folder()
    {
        var session = await CreateAsync();
        var target = Directory.CreateDirectory(Path.Combine(Workspace, "Yeni Klasör"));

        (await _service.RelocateAsync(session.Id, target.FullName)).Value.WorkspacePath.ShouldBe(target.FullName);
        _repository.Items.Single().WorkspacePath.ShouldBe(target.FullName);

        (await _service.RelocateAsync(session.Id, Path.Combine(Workspace, "yok"))).Error!.Code.ShouldBe("WorkspaceMissing");
        _repository.Items.Single().WorkspacePath.ShouldBe(target.FullName);
    }

    [Fact]
    public async Task Reconcile_takes_last_activity_from_the_transcript_time()
    {
        var withHistory = await OpenedSessionAsync(withHistory: false);
        var without = await OpenedSessionAsync(withHistory: false);
        var unreadable = await OpenedSessionAsync(withHistory: false);
        var transcriptTime = _now.AddDays(-3);
        _history.Find(withHistory.ClaudeSessionId).Returns(new HistoryInfo(transcriptTime));
        _history.Find(unreadable.ClaudeSessionId).Throws(new UnauthorizedAccessException());
        int changes = 0;
        _service.Changed += (_, _) => changes++;

        (await _service.ReconcileAsync()).ShouldBe(1);
        (await _service.ReconcileAsync()).ShouldBe(0);

        _repository.Items.Single(s => s.Id == withHistory.Id).LastActivityAt.ShouldBe(transcriptTime);
        _repository.Items.Single(s => s.Id == without.Id).LastActivityAt.ShouldBe(without.LastActivityAt);
        changes.ShouldBe(1);
    }

    [Fact]
    public async Task Unreadable_history_is_not_reported_as_missing()
    {
        var session = await OpenedSessionAsync(withHistory: false);
        _history.Find(session.ClaudeSessionId).Throws(new IOException("locked"));

        _service.GetState(session).ShouldBe(SessionState.Closed);
    }

    [Fact]
    public async Task List_returns_each_session_with_its_state()
    {
        var closed = await CreateAsync();
        var open = await OpenedSessionAsync(withHistory: true);
        var error = await OpenedSessionAsync(withHistory: true);
        await _repository.UpdateAsync(error with { LastErrorCode = "DeleteFailed" });
        _running.Add(open.ClaudeSessionId);

        var states = (await _service.ListAsync()).ToDictionary(item => item.Session.Id, item => item.State);

        states[closed.Id].ShouldBe(SessionState.Closed);
        states[open.Id].ShouldBe(SessionState.Open);
        states[error.Id].ShouldBe(SessionState.Error);
    }

    [Fact]
    public void Monitor_changes_are_forwarded()
    {
        int changes = 0;
        _service.Changed += (_, _) => changes++;

        _monitor.Changed += Raise.Event();

        changes.ShouldBe(1);
    }

    private async Task<Session> CreateAsync() => (await _service.CreateAsync("Dava", Workspace, null)).Value;

    private async Task<Session> OpenedSessionAsync(bool withHistory)
    {
        var session = await CreateAsync() with
        {
            LastOpenedAt = _now.AddDays(-1),
            LastActivityAt = _now.AddDays(-1),
            ClaudeVersion = "1.0.0",
        };
        await _repository.UpdateAsync(session);
        if (withHistory)
        {
            _history.Find(session.ClaudeSessionId).Returns(new HistoryInfo(_now.AddDays(-1)));
        }
        return session;
    }

    public void Dispose()
    {
        _service.Dispose();
        _root.Delete(recursive: true);
    }

    private sealed class InMemorySessionRepository : ISessionRepository
    {
        private readonly Dictionary<Guid, Session> _items = [];

        public IReadOnlyCollection<Session> Items
        {
            get
            {
                lock (_items)
                {
                    return [.. _items.Values];
                }
            }
        }

        public Task<Session?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            lock (_items)
            {
                return Task.FromResult(_items.GetValueOrDefault(id));
            }
        }

        public Task<IReadOnlyList<Session>> ListAsync(CancellationToken cancellationToken = default)
        {
            lock (_items)
            {
                return Task.FromResult<IReadOnlyList<Session>>([.. _items.Values]);
            }
        }

        public Task AddAsync(Session session, CancellationToken cancellationToken = default)
        {
            lock (_items)
            {
                _items.Add(session.Id, session);
            }
            return Task.CompletedTask;
        }

        public Task<bool> UpdateAsync(Session session, CancellationToken cancellationToken = default)
        {
            lock (_items)
            {
                if (!_items.ContainsKey(session.Id))
                {
                    return Task.FromResult(false);
                }
                _items[session.Id] = session;
                return Task.FromResult(true);
            }
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            lock (_items)
            {
                return Task.FromResult(_items.Remove(id));
            }
        }
    }
}
