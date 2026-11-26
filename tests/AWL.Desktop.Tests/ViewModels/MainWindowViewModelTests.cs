using AWL.Core.Categories;
using AWL.Core.Claude;
using AWL.Core.Sessions;
using AWL.Desktop.Messages;
using AWL.Desktop.Resources;
using AWL.Desktop.Services;
using AWL.Desktop.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using NSubstitute;

namespace AWL.Desktop.Tests.ViewModels;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly SessionFixture _fixture = new();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IShellService _shell = Substitute.For<IShellService>();
    private readonly ManualDispatcher _dispatcher = new();
    private readonly StrongReferenceMessenger _messenger = new();
    private readonly MainWindowViewModel _viewModel;

    public MainWindowViewModelTests()
    {
        _shell.OpenFolder(Arg.Any<string>()).Returns(true);
        _viewModel = new MainWindowViewModel(_fixture.Service, _fixture.Categories, _dialogs, _shell, _dispatcher, _messenger);
    }

    [Fact]
    public async Task Sessions_are_grouped_by_category_then_uncategorized_then_archive()
    {
        var work = new Category(Guid.NewGuid(), "İş", 1);
        var home = new Category(Guid.NewGuid(), "Ev", 0);
        await _fixture.Categories.AddAsync(work);
        await _fixture.Categories.AddAsync(home);
        await _fixture.AddAsync("Sözleşme", work.Id);
        await _fixture.AddAsync("Kira", home.Id);
        await _fixture.AddAsync("Not");
        await _fixture.AddAsync("Eski", work.Id, archived: true);
        await _fixture.AddAsync("Silinmiş kategori", Guid.NewGuid());

        await _viewModel.RefreshAsync();

        var groups = _viewModel.Sessions.OrderBy(item => item.GroupOrder)
            .Select(item => (item.Title, item.GroupName)).ToList();
        groups.ShouldBe(
        [
            ("Kira", "Ev"),
            ("Sözleşme", "İş"),
            ("Not", Strings.UncategorizedGroup),
            ("Silinmiş kategori", Strings.UncategorizedGroup),
            ("Eski", Strings.ArchiveGroup),
        ], ignoreOrder: true);
        _viewModel.Sessions.Single(i => i.Title == "Kira").GroupOrder
            .ShouldBeLessThan(_viewModel.Sessions.Single(i => i.Title == "Sözleşme").GroupOrder);
        _viewModel.Sessions.Single(i => i.Title == "Not").GroupOrder
            .ShouldBeLessThan(_viewModel.Sessions.Single(i => i.Title == "Eski").GroupOrder);
        _viewModel.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public async Task Refresh_updates_rows_in_place_and_keeps_the_selection()
    {
        var older = await _fixture.AddAsync("Eski", lastActivity: _fixture.Now.AddDays(-5));
        var newer = await _fixture.AddAsync("Yeni", lastActivity: _fixture.Now.AddDays(-1));
        await _viewModel.RefreshAsync();
        _viewModel.SelectedSession!.Id.ShouldBe(newer.Id);
        _viewModel.SelectedSession = _viewModel.Sessions.Single(item => item.Id == older.Id);
        var row = _viewModel.SelectedSession;

        await _fixture.Repository.UpdateAsync(older with { Title = "Yeniden adlandırıldı" });
        _fixture.Running.Add(older.ClaudeSessionId);
        await _viewModel.RefreshAsync();

        _viewModel.SelectedSession.ShouldBeSameAs(row);
        row.Title.ShouldBe("Yeniden adlandırıldı");
        row.State.ShouldBe(SessionState.Open);
        row.StateText.ShouldBe(Strings.State_Open);

        await _fixture.Repository.DeleteAsync(older.Id);
        await _viewModel.RefreshAsync();
        _viewModel.Sessions.ShouldHaveSingleItem().Id.ShouldBe(newer.Id);
        _viewModel.SelectedSession!.Id.ShouldBe(newer.Id);
    }

    [Fact]
    public async Task An_empty_list_reports_empty_and_disables_session_commands()
    {
        await _viewModel.RefreshAsync();

        _viewModel.IsEmpty.ShouldBeTrue();
        _viewModel.HasSelection.ShouldBeFalse();
        _viewModel.OpenCommand.CanExecute(null).ShouldBeFalse();
        _viewModel.OpenFolderCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task Change_messages_are_coalesced_into_one_refresh_on_the_ui_thread()
    {
        await _viewModel.RefreshAsync();
        await _fixture.AddAsync("Yeni");

        _messenger.Send(new SessionsChangedMessage());
        _messenger.Send(new SessionsChangedMessage());
        _messenger.Send(new SessionsChangedMessage());

        _dispatcher.Pending.ShouldBe(1);
        _viewModel.Sessions.ShouldBeEmpty();
        await _dispatcher.RunAllAsync();
        _viewModel.Sessions.ShouldHaveSingleItem().Title.ShouldBe("Yeni");

        _messenger.Send(new SessionsChangedMessage());
        _dispatcher.Pending.ShouldBe(1);
    }

    [Fact]
    public async Task Open_launches_the_selected_session()
    {
        await SelectAsync(await _fixture.AddAsync("Dava"));

        await _viewModel.OpenCommand.ExecuteAsync(null);

        _fixture.Launches.ShouldHaveSingleItem();
        _dialogs.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Opening_a_running_session_tells_the_user()
    {
        var session = await _fixture.AddAsync("Dava", opened: true);
        _fixture.Running.Add(session.ClaudeSessionId);
        await SelectAsync(session);

        await _viewModel.OpenCommand.ExecuteAsync(null);

        _fixture.Launches.ShouldBeEmpty();
        _dialogs.Received(1).ShowInfo(Strings.SessionAlreadyOpen);
        await _fixture.Monitor.Received(1).TryBringToFrontAsync(session.ClaudeSessionId, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Missing_history_offers_a_new_conversation_in_the_same_folder(bool accept)
    {
        var session = await _fixture.AddAsync("Dava", opened: true);
        await SelectAsync(session);
        _dialogs.Confirm(Strings.HistoryMissingStartOver).Returns(accept);

        await _viewModel.OpenCommand.ExecuteAsync(null);

        var stored = _fixture.Repository.Items.Single();
        if (accept)
        {
            stored.ClaudeSessionId.ShouldNotBe(session.ClaudeSessionId);
            _fixture.Launches.ShouldHaveSingleItem().Arguments[0].ShouldBe("--session-id");
        }
        else
        {
            stored.ClaudeSessionId.ShouldBe(session.ClaudeSessionId);
            _fixture.Launches.ShouldBeEmpty();
        }
        _dialogs.DidNotReceive().ShowError(Arg.Any<string>());
    }

    [Fact]
    public async Task Missing_folder_lets_the_user_pick_a_new_one_and_opens_there()
    {
        var gone = Path.Combine(_fixture.Root, "Silinen");
        var target = Directory.CreateDirectory(Path.Combine(_fixture.Root, "Yeni")).FullName;
        await SelectAsync(await _fixture.AddAsync("Dava", folder: gone));
        _dialogs.Confirm(Strings.WorkspaceMissingPickFolder).Returns(true);
        _dialogs.PickFolder(Arg.Any<string?>()).Returns(target);

        await _viewModel.OpenCommand.ExecuteAsync(null);

        _fixture.Repository.Items.Single().WorkspacePath.ShouldBe(target);
        _fixture.Launches.ShouldHaveSingleItem().WorkingDirectory.ShouldBe(target);
    }

    [Fact]
    public async Task Other_failures_are_shown_with_their_message()
    {
        await SelectAsync(await _fixture.AddAsync("Dava"));
        _fixture.Terminal.LaunchAsync(Arg.Any<AWL.Core.Terminal.ClaudeLaunchSpec>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(AWL.Core.Common.Result.Fail(new AWL.Core.Common.AppError("TerminalLaunchFailed", "TerminalLaunchFailed")));

        await _viewModel.OpenCommand.ExecuteAsync(null);

        _dialogs.Received(1).ShowError(Strings.TerminalLaunchFailed);
    }

    [Fact]
    public async Task Folder_actions_open_explorer_and_create_the_outputs_folder()
    {
        await SelectAsync(await _fixture.AddAsync("Dava"));

        _viewModel.OpenFolderCommand.Execute(null);
        _viewModel.OpenOutputsCommand.Execute(null);

        string outputs = Path.Combine(_fixture.Root, MainWindowViewModel.OutputsFolderName);
        Directory.Exists(outputs).ShouldBeTrue();
        Received.InOrder(() =>
        {
            _shell.OpenFolder(_fixture.Root);
            _shell.OpenFolder(outputs);
        });
    }

    [Fact]
    public async Task Folder_actions_do_not_recreate_a_vanished_workspace()
    {
        var gone = Path.Combine(_fixture.Root, "Silinen");
        await SelectAsync(await _fixture.AddAsync("Dava", folder: gone));

        _viewModel.OpenFolderCommand.Execute(null);
        _viewModel.OpenOutputsCommand.Execute(null);

        Directory.Exists(gone).ShouldBeFalse();
        _shell.DidNotReceive().OpenFolder(Arg.Any<string>());
        _dialogs.Received(2).ShowError(Strings.WorkspaceMissing);
    }

    [Fact]
    public async Task Initialize_reconciles_activity_from_transcripts_before_listing()
    {
        var session = await _fixture.AddAsync("Dava", opened: true);
        var transcript = _fixture.Now.AddHours(-3);
        _fixture.History.Find(session.ClaudeSessionId).Returns(new HistoryInfo(transcript));

        await _viewModel.InitializeAsync();

        _viewModel.Sessions.Single().Session.LastActivityAt.ShouldBe(transcript);
    }

    [Fact]
    public async Task New_session_is_created_selected_and_opened()
    {
        await _fixture.AddAsync("Mevcut");
        await _viewModel.RefreshAsync();
        _viewModel.ShowToolsCommand.Execute(null);
        _dialogs.ShowSessionEditor(Arg.Any<SessionEditorViewModel>()).Returns(call =>
        {
            var editor = call.Arg<SessionEditorViewModel>();
            editor.WorkspacePath.ShouldBe(_fixture.Root);
            editor.Title = "Yeni Dava";
            editor.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            return editor.SavedSession is not null;
        });

        await _viewModel.NewSessionAsync(_fixture.Root);

        _viewModel.SelectedSession!.Title.ShouldBe("Yeni Dava");
        _viewModel.IsSessionsPage.ShouldBeTrue();
        _fixture.Launches.ShouldHaveSingleItem().Arguments[0].ShouldBe("--session-id");
    }

    [Fact]
    public async Task Cancelled_new_session_changes_nothing()
    {
        _dialogs.ShowSessionEditor(Arg.Any<SessionEditorViewModel>()).Returns(false);

        await _viewModel.NewSessionCommand.ExecuteAsync(null);

        _fixture.Repository.Items.ShouldBeEmpty();
        _fixture.Launches.ShouldBeEmpty();
    }

    [Fact]
    public async Task Edit_opens_the_editor_for_the_selected_session_and_refreshes()
    {
        var session = await _fixture.AddAsync("Eski");
        await SelectAsync(session);
        _dialogs.ShowSessionEditor(Arg.Any<SessionEditorViewModel>()).Returns(call =>
        {
            var editor = call.Arg<SessionEditorViewModel>();
            editor.IsNew.ShouldBeFalse();
            editor.Title = "Yeni";
            editor.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            return true;
        });

        await _viewModel.EditCommand.ExecuteAsync(null);

        _viewModel.SelectedSession!.Title.ShouldBe("Yeni");
        _fixture.Launches.ShouldBeEmpty();
    }

    [Fact]
    public async Task Archive_toggles_and_moves_the_session_between_groups()
    {
        await SelectAsync(await _fixture.AddAsync("Dava"));
        _viewModel.SelectedSession!.ArchiveActionText.ShouldBe(Strings.ArchiveButton);

        await _viewModel.ToggleArchiveCommand.ExecuteAsync(null);
        _viewModel.SelectedSession.GroupName.ShouldBe(Strings.ArchiveGroup);
        _viewModel.SelectedSession.State.ShouldBe(SessionState.Archived);
        _viewModel.SelectedSession.ArchiveActionText.ShouldBe(Strings.UnarchiveButton);

        await _viewModel.ToggleArchiveCommand.ExecuteAsync(null);
        _viewModel.SelectedSession.GroupName.ShouldBe(Strings.UncategorizedGroup);
    }

    [Fact]
    public void Navigation_switches_between_sessions_and_tools()
    {
        _viewModel.IsSessionsPage.ShouldBeTrue();
        _viewModel.ShowToolsCommand.Execute(null);
        _viewModel.IsToolsPage.ShouldBeTrue();
        _viewModel.IsSessionsPage.ShouldBeFalse();
        _viewModel.ShowSessionsCommand.Execute(null);
        _viewModel.CurrentPage.ShouldBe(MainPage.Sessions);
    }

    private async Task SelectAsync(Session session)
    {
        await _viewModel.RefreshAsync();
        _viewModel.SelectedSession = _viewModel.Sessions.Single(item => item.Id == session.Id);
    }

    public void Dispose() => _fixture.Dispose();
}
