using AWL.Core.Categories;
using AWL.Core.Shell;
using AWL.Desktop.Resources;
using AWL.Desktop.Services;
using AWL.Desktop.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using NSubstitute;

namespace AWL.Desktop.Tests.Services;

public sealed class LaunchRequestCoordinatorTests : IDisposable
{
    private readonly SessionFixture _fixture = new();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IWindowActivator _activator = Substitute.For<IWindowActivator>();
    private readonly IClipboardService _clipboard = Substitute.For<IClipboardService>();
    private readonly ILaunchRequestSource _source = Substitute.For<ILaunchRequestSource>();
    private readonly ManualDispatcher _dispatcher = new();
    private readonly List<string> _editorFolders = [];
    private readonly LaunchRequestCoordinator _coordinator;
    private readonly string _file;

    public LaunchRequestCoordinatorTests()
    {
        _file = Path.Combine(_fixture.Root, "Dilekçe ş.udf");
        File.WriteAllText(_file, "x");
        _clipboard.TrySetText(Arg.Any<string>()).Returns(true);
        _dialogs.ShowSessionEditor(Arg.Any<SessionEditorViewModel>()).Returns(call =>
        {
            _editorFolders.Add(call.Arg<SessionEditorViewModel>().WorkspacePath);
            return false;
        });
        var main = new MainWindowViewModel(_fixture.Service, _fixture.Deletion, new CategoryService(_fixture.Categories),
            _dialogs, Substitute.For<IShellService>(), _dispatcher, new StrongReferenceMessenger());
        _coordinator = new LaunchRequestCoordinator(_source, main, _dispatcher, _activator, _clipboard, _dialogs);
    }

    private LaunchRequest Folder => LaunchRequest.Create(LaunchKind.Folder, _fixture.Root).Value;

    private LaunchRequest FileRequest => LaunchRequest.Create(LaunchKind.File, _file).Value;

    [Fact]
    public async Task A_plain_start_only_brings_the_window_forward()
    {
        await _coordinator.MarkReadyAsync();
        await _coordinator.SubmitAsync(LaunchRequest.Activate);

        _activator.Received(1).Activate();
        _editorFolders.ShouldBeEmpty();
    }

    [Fact]
    public async Task Folder_requests_wait_for_setup_then_open_the_new_session_window_with_the_folder()
    {
        await _coordinator.SubmitAsync(Folder);
        _activator.Received(1).Activate();
        _editorFolders.ShouldBeEmpty();

        await _coordinator.MarkReadyAsync();

        _editorFolders.ShouldBe([_fixture.Root]);
    }

    [Fact]
    public async Task File_requests_copy_the_quoted_path_and_use_the_file_folder()
    {
        await _coordinator.MarkReadyAsync();
        await _coordinator.SubmitAsync(FileRequest);

        _clipboard.Received(1).TrySetText($"\"{_file}\"");
        _editorFolders.ShouldBe([_fixture.Root]);
        _dialogs.Received(1).ShowInfo(Strings.FilePathCopied);
    }

    [Fact]
    public async Task A_locked_clipboard_still_opens_the_window_without_claiming_a_copy()
    {
        _clipboard.TrySetText(Arg.Any<string>()).Returns(false);
        await _coordinator.MarkReadyAsync();
        await _coordinator.SubmitAsync(FileRequest);

        _editorFolders.ShouldHaveSingleItem();
        _dialogs.DidNotReceive().ShowInfo(Arg.Any<string>());
    }

    [Fact]
    public async Task Forwarded_requests_run_on_the_ui_thread()
    {
        await _coordinator.MarkReadyAsync();

        _source.RequestReceived += Raise.Event<EventHandler<LaunchRequest>>(_source, Folder);

        _editorFolders.ShouldBeEmpty();
        await _dispatcher.RunAllAsync();
        _editorFolders.ShouldBe([_fixture.Root]);
    }

    [Fact]
    public async Task A_request_arriving_while_the_window_is_open_waits_its_turn()
    {
        await _coordinator.MarkReadyAsync();
        var second = LaunchRequest.Create(LaunchKind.Folder, Directory.CreateDirectory(
            Path.Combine(_fixture.Root, "İkinci")).FullName).Value;
        _dialogs.ShowSessionEditor(Arg.Any<SessionEditorViewModel>()).Returns(call =>
        {
            var folder = call.Arg<SessionEditorViewModel>().WorkspacePath;
            _editorFolders.Add(folder);
            if (_editorFolders.Count == 1)
            {
                // The modal window pumps messages, so a nested request can arrive here.
                _coordinator.SubmitAsync(second).GetAwaiter().GetResult();
                _editorFolders.Count.ShouldBe(1);
            }
            return false;
        });

        await _coordinator.SubmitAsync(Folder);

        _editorFolders.ShouldBe([_fixture.Root, second.Path!]);
    }

    public void Dispose()
    {
        _coordinator.Dispose();
        _fixture.Dispose();
    }
}
