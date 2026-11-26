using AWL.Core.Claude;
using AWL.Core.Common;
using AWL.Core.Configuration;
using AWL.Core.Retention;
using AWL.Core.Sessions;
using NSubstitute;

namespace AWL.Core.Tests.Retention;

public sealed class RetentionPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(-31, 30, true)]
    [InlineData(-29, 30, false)]
    [InlineData(-3651, 3650, true)]
    [InlineData(-1, 3650, false)]
    public void Expiry_uses_the_last_activity(int activityDaysAgo, int retention, bool expired)
    {
        var session = Create(Now.AddYears(-20), Now.AddDays(activityDaysAgo));
        RetentionPolicy.IsExpired(session, retention, Now).ShouldBe(expired);
    }

    [Fact]
    public void A_never_used_session_expires_from_its_creation()
    {
        RetentionPolicy.IsExpired(Create(Now.AddDays(-40), null), 30, Now).ShouldBeTrue();
        RetentionPolicy.IsExpired(Create(Now.AddDays(-10), null), 30, Now).ShouldBeFalse();
    }

    [Fact]
    public void Periods_are_validated_and_huge_values_do_not_overflow()
    {
        RetentionPolicy.IsValid(0).ShouldBeFalse();
        RetentionPolicy.IsValid(1).ShouldBeTrue();
        RetentionPolicy.IsValid(RetentionPolicy.MaxDays + 1).ShouldBeFalse();
        RetentionPolicy.IsExpired(Create(Now.AddYears(-50), null), int.MaxValue, Now).ShouldBeFalse();
        RetentionPolicy.IsExpired(Create(Now.AddYears(-150), null), int.MaxValue, Now).ShouldBeTrue();
        Should.Throw<ArgumentOutOfRangeException>(() => RetentionPolicy.IsExpired(Create(Now, null), 0, Now));
    }

    internal static Session Create(DateTimeOffset created, DateTimeOffset? activity) =>
        new(Guid.NewGuid(), "Dava", null, @"C:\İş", Guid.NewGuid(), null, false, created, activity, activity);
}

public sealed class SessionDeletionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private readonly ISessionRepository _repository = Substitute.For<ISessionRepository>();
    private readonly IClaudeHistoryStore _history = Substitute.For<IClaudeHistoryStore>();
    private readonly IClaudeProcessMonitor _monitor = Substitute.For<IClaudeProcessMonitor>();
    private readonly IAwlSettingsStore _settings = Substitute.For<IAwlSettingsStore>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly HashSet<Guid> _running = [];
    private readonly Dictionary<Guid, Session> _stored = [];
    private readonly SessionDeletionService _service;
    private int _changes;

    public SessionDeletionServiceTests()
    {
        _clock.UtcNow.Returns(Now);
        _monitor.RunningSessionIds.Returns(_running);
        _settings.Load().Returns(new AwlSettings { RetentionDays = 30 });
        _repository.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => _stored.GetValueOrDefault(call.Arg<Guid>()));
        _repository.ListAsync(Arg.Any<CancellationToken>()).Returns(_ => (IReadOnlyList<Session>)[.. _stored.Values]);
        _repository.UpdateAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var session = call.Arg<Session>();
            _stored[session.Id] = session;
            return true;
        });
        _repository.DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call => _stored.Remove(call.Arg<Guid>()));
        _history.DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Complete);
        _monitor.StopSessionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            _running.Remove(call.Arg<Guid>());
            return Result.Ok();
        });
        _service = new SessionDeletionService(_repository, _history, _monitor, _settings, _clock) { SettleDelay = TimeSpan.Zero };
        _service.Changed += (_, _) => _changes++;
    }

    private static DeleteReport Complete => new([]);

    private static DeleteReport Partial => new([new DeleteFailure("projects/x.jsonl", "in use")]);

    [Fact]
    public async Task Claude_files_are_deleted_before_the_record()
    {
        var session = Add(Now);

        (await _service.DeleteAsync(session.Id, stopIfRunning: false)).IsSuccess.ShouldBeTrue();

        Received.InOrder(() =>
        {
            _history.DeleteAsync(session.ClaudeSessionId, Arg.Any<CancellationToken>());
            _repository.DeleteAsync(session.Id, Arg.Any<CancellationToken>());
        });
        _stored.ShouldBeEmpty();
        _changes.ShouldBe(1);
    }

    [Fact]
    public async Task Partial_failure_keeps_the_record_in_error_and_a_retry_completes_it()
    {
        var session = Add(Now);
        _history.DeleteAsync(session.ClaudeSessionId, Arg.Any<CancellationToken>()).Returns(Partial, Complete);

        var failed = await _service.DeleteAsync(session.Id, stopIfRunning: false);

        failed.Error!.Code.ShouldBe(SessionDeletionService.DeleteIncompleteCode);
        _stored[session.Id].LastErrorCode.ShouldBe(SessionDeletionService.DeleteIncompleteCode);
        await _repository.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        (await _service.DeleteAsync(session.Id, stopIfRunning: false)).IsSuccess.ShouldBeTrue();
        _stored.ShouldBeEmpty();
        _changes.ShouldBe(2);
    }

    [Fact]
    public async Task An_open_session_is_not_deleted_without_permission_to_stop_it()
    {
        var session = Add(Now);
        _running.Add(session.ClaudeSessionId);

        (await _service.DeleteAsync(session.Id, stopIfRunning: false)).Error!.Code.ShouldBe("SessionOpen");

        await _monitor.DidNotReceive().StopSessionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _history.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        _stored.ShouldContainKey(session.Id);
    }

    [Fact]
    public async Task An_open_session_is_stopped_then_deleted_retrying_files_it_still_held()
    {
        var session = Add(Now);
        _running.Add(session.ClaudeSessionId);
        _history.DeleteAsync(session.ClaudeSessionId, Arg.Any<CancellationToken>()).Returns(Partial, Complete);

        (await _service.DeleteAsync(session.Id, stopIfRunning: true)).IsSuccess.ShouldBeTrue();

        await _monitor.Received(1).StopSessionAsync(session.ClaudeSessionId, Arg.Any<CancellationToken>());
        await _history.Received(2).DeleteAsync(session.ClaudeSessionId, Arg.Any<CancellationToken>());
        _stored.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failed_stop_deletes_nothing()
    {
        var session = Add(Now);
        _running.Add(session.ClaudeSessionId);
        _monitor.StopSessionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail(new AppError("ClaudeStopFailed", "ClaudeStopFailed")));

        (await _service.DeleteAsync(session.Id, stopIfRunning: true)).Error!.Code.ShouldBe("ClaudeStopFailed");

        await _history.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        _stored.ShouldContainKey(session.Id);
    }

    [Fact]
    public async Task Missing_sessions_are_reported()
    {
        (await _service.DeleteAsync(Guid.NewGuid(), stopIfRunning: true)).Error!.Code.ShouldBe("SessionNotFound");
    }

    [Fact]
    public async Task Purge_deletes_expired_closed_sessions_only()
    {
        var expired = Add(Now.AddDays(-31));
        var archivedExpired = Add(Now.AddDays(-400), archived: true);
        var recent = Add(Now.AddDays(-5));
        var openExpired = Add(Now.AddDays(-90));
        var stuck = Add(Now.AddDays(-60));
        _running.Add(openExpired.ClaudeSessionId);
        _history.DeleteAsync(stuck.ClaudeSessionId, Arg.Any<CancellationToken>()).Returns(Partial);

        (await _service.PurgeExpiredAsync()).ShouldBe(2);

        _stored.Keys.ShouldBe([recent.Id, openExpired.Id, stuck.Id], ignoreOrder: true);
        _stored[stuck.Id].LastErrorCode.ShouldBe(SessionDeletionService.DeleteIncompleteCode);
        _stored.ShouldNotContainKey(expired.Id);
        _stored.ShouldNotContainKey(archivedExpired.Id);
        await _monitor.DidNotReceive().StopSessionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private Session Add(DateTimeOffset activity, bool archived = false)
    {
        var session = RetentionPolicyTests.Create(activity.AddDays(-1), activity) with { IsArchived = archived };
        _stored.Add(session.Id, session);
        return session;
    }
}
