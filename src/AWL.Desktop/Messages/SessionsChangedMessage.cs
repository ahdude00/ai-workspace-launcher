using AWL.Core.Categories;
using AWL.Core.Sessions;
using CommunityToolkit.Mvvm.Messaging;

namespace AWL.Desktop.Messages;

/// <summary>Published when a session or the set of running sessions changed; receivers reload their view.</summary>
public sealed record SessionsChangedMessage;

/// <summary>
/// Publishes <see cref="SessionService.Changed"/> and <see cref="CategoryService.Changed"/> (group names and order)
/// as <see cref="SessionsChangedMessage"/> (Teknik Tasarım §03).
/// </summary>
internal sealed class SessionChangeRelay : IDisposable
{
    private readonly SessionService _sessions;
    private readonly CategoryService _categories;
    private readonly IMessenger _messenger;

    public SessionChangeRelay(SessionService sessions, CategoryService categories, IMessenger messenger)
    {
        _sessions = sessions;
        _categories = categories;
        _messenger = messenger;
        _sessions.Changed += OnChanged;
        _categories.Changed += OnChanged;
    }

    public void Dispose()
    {
        _sessions.Changed -= OnChanged;
        _categories.Changed -= OnChanged;
    }

    private void OnChanged(object? sender, EventArgs e) => _messenger.Send(new SessionsChangedMessage());
}
