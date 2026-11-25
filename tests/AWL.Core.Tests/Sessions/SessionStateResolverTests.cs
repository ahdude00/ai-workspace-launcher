using AWL.Core.Sessions;

namespace AWL.Core.Tests.Sessions;

public sealed class SessionStateResolverTests
{
    private static readonly Session Never = new(Guid.NewGuid(), "Dava", null, @"C:\İş", Guid.NewGuid(), null, false,
        DateTimeOffset.UnixEpoch, null, null);

    private static readonly Session Opened = Never with { LastOpenedAt = DateTimeOffset.UnixEpoch };

    public static TheoryData<Session, bool, bool, bool, SessionState> Cases => new()
    {
        // session, running, history, error → state
        { Never, false, false, false, SessionState.Closed },
        { Opened, false, true, false, SessionState.Closed },
        { Opened, false, false, false, SessionState.HistoryMissing },
        { Opened, true, false, false, SessionState.Open },
        { Opened, true, true, false, SessionState.Open },
        { Opened with { IsArchived = true }, false, true, false, SessionState.Archived },
        { Opened with { IsArchived = true }, false, false, false, SessionState.Archived },
        { Opened with { IsArchived = true }, true, true, false, SessionState.Open },
        { Opened, true, true, true, SessionState.Error },
        { Never with { IsArchived = true }, false, false, true, SessionState.Error },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Resolve_applies_the_precedence_error_open_archived_missing_closed(
        Session session, bool running, bool history, bool error, SessionState expected)
    {
        SessionStateResolver.Resolve(session, running, history, error).ShouldBe(expected);
    }

    [Fact]
    public void Resolve_rejects_null()
    {
        Should.Throw<ArgumentNullException>(() => SessionStateResolver.Resolve(null!, false, false, false));
    }
}
