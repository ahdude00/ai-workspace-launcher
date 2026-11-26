using AWL.Desktop.Resources;
using AWL.Desktop.Services;
using AWL.Desktop.ViewModels;
using NSubstitute;

namespace AWL.Desktop.Tests.ViewModels;

public sealed class DropZoneViewModelTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("awl-dropzone-");
    private readonly IClipboardService _clipboard = Substitute.For<IClipboardService>();
    private readonly DropZoneViewModel _drop;
    private readonly string _a;
    private readonly string _b;

    public DropZoneViewModelTests()
    {
        _a = Path.Combine(_root.FullName, "Dilekçe ş.docx");
        _b = Path.Combine(_root.FullName, "Rapor; son.pdf");
        File.WriteAllText(_a, "x");
        File.WriteAllText(_b, "x");
        _clipboard.TrySetText(Arg.Any<string>()).Returns(true);
        _drop = new DropZoneViewModel(_clipboard);
    }

    [Fact]
    public void Starts_with_the_hint()
    {
        _drop.Message.ShouldBe(Strings.DropHint);
        _drop.IsError.ShouldBeFalse();
    }

    [Fact]
    public void All_dropped_paths_go_to_the_clipboard_quoted()
    {
        _drop.Drop([_a, _b]);

        _clipboard.Received(1).TrySetText($"\"{_a}\" \"{_b}\"");
        _drop.Message.ShouldStartWith("2 ");
        _drop.Message.ShouldContain("Ctrl+V");
        _drop.IsError.ShouldBeFalse();
    }

    [Fact]
    public void Skipped_items_are_mentioned()
    {
        _drop.Drop([_a, Path.Combine(_root.FullName, "yok.pdf")]);

        _clipboard.Received(1).TrySetText($"\"{_a}\"");
        _drop.Message.ShouldContain("1 öğe atlandı");
    }

    [Fact]
    public void Unusable_drops_and_a_busy_clipboard_are_errors()
    {
        _drop.Drop(["göreli.pdf"]);
        _drop.Message.ShouldBe(Strings.DropNothingUsable);
        _drop.IsError.ShouldBeTrue();
        _clipboard.DidNotReceive().TrySetText(Arg.Any<string>());

        _clipboard.TrySetText(Arg.Any<string>()).Returns(false);
        _drop.Drop([_a]);
        _drop.Message.ShouldBe(Strings.CopyFailed);
        _drop.IsError.ShouldBeTrue();
    }

    public void Dispose() => _root.Delete(recursive: true);
}
