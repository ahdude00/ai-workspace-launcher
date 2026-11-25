using AWL.Core.Sessions;
using CommunityToolkit.Mvvm.Messaging;

namespace AWL.Desktop.Messages;

/// <summary>Published when a session or the set of running sessions changed; receivers reload their view.</summary>
public sealed record SessionsChangedMessage;

/// <summary>Publishes <see cref="SessionService.Changed"/> as <see cref="SessionsChangedMessage"/> (Teknik Tasarım §03).</summary>
internal sealed class SessionChangeRelay : IDisposable
{
    private readonly SessionService _sessions;
    private readonly IMessenger _messenger;

    public SessionChangeRelay(SessionService sessions, IMessenger messenger)
    {
        _sessions = sessions;
        _messenger = messenger;
        _sessions.Changed += OnChanged;
    }

    public void Dispose() => _sessions.Changed -= OnChanged;

    private void OnChanged(object? sender, EventArgs e) => _messenger.Send(new SessionsChangedMessage());
}
