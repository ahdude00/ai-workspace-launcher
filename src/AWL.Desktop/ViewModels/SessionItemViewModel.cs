using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using AWL.Core.Sessions;
using AWL.Desktop.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AWL.Desktop.ViewModels;

/// <summary>One row of the session list. Updated in place so that selection and scroll position survive a refresh.</summary>
public sealed partial class SessionItemViewModel : ObservableObject
{
    public SessionItemViewModel(SessionSnapshot snapshot, string groupName, int groupOrder)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Id = snapshot.Session.Id;
        Update(snapshot, groupName, groupOrder);
    }

    public Guid Id { get; }

    [ObservableProperty]
    public partial Session Session { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText), nameof(AccessibleName))]
    public partial SessionState State { get; private set; }

    /// <summary>The list group: the category name, "Kategorisiz" or "Arşiv".</summary>
    [ObservableProperty]
    public partial string GroupName { get; private set; }

    /// <summary>Orders the groups: categories in their order, then uncategorized, then the archive.</summary>
    [ObservableProperty]
    public partial int GroupOrder { get; private set; }

    /// <summary>Most recently active sessions come first within a group.</summary>
    [ObservableProperty]
    public partial DateTimeOffset SortKey { get; private set; }

    public string Title => Session.Title;

    public string WorkspacePath => Session.WorkspacePath;

    public string StateText => Strings.ResourceManager.GetString("State_" + State, CultureInfo.CurrentUICulture)
        ?? State.ToString();

    public string LastActivityText => Session.LastActivityAt is { } time
        ? time.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
        : Strings.NeverOpened;

    public bool IsArchived => Session.IsArchived;

    public string ArchiveActionText => IsArchived ? Strings.UnarchiveButton : Strings.ArchiveButton;

    public string AccessibleName => $"{Title}, {StateText}";

    [MemberNotNull(nameof(Session), nameof(GroupName))]
    internal void Update(SessionSnapshot snapshot, string groupName, int groupOrder)
    {
        Session = snapshot.Session;
        State = snapshot.State;
        GroupName = groupName;
        GroupOrder = groupOrder;
        SortKey = snapshot.Session.LastActivityAt ?? snapshot.Session.CreatedAt;
    }

    partial void OnSessionChanged(Session value)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(WorkspacePath));
        OnPropertyChanged(nameof(LastActivityText));
        OnPropertyChanged(nameof(AccessibleName));
        OnPropertyChanged(nameof(IsArchived));
        OnPropertyChanged(nameof(ArchiveActionText));
    }
}
