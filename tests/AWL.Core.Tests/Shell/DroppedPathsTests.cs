using AWL.Core.Shell;

namespace AWL.Core.Tests.Shell;

public sealed class DroppedPathsTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("awl-drop-");
    private readonly string _first;
    private readonly string _second;
    private readonly string _folder;

    public DroppedPathsTests()
    {
        _folder = Directory.CreateDirectory(Path.Combine(_root.FullName, "Ek Belgeler; 2026")).FullName;
        _first = Path.Combine(_root.FullName, "Cevap Dilekçesi.docx");
        _second = Path.Combine(_folder, "Bilirkişi Raporu (ş).pdf");
        File.WriteAllText(_first, "x");
        File.WriteAllText(_second, "x");
    }

    [Fact]
    public void All_files_and_folders_are_quoted_and_space_separated()
    {
        var dropped = DroppedPaths.Format([_first, _second, _folder]).Value;

        dropped.Text.ShouldBe($"\"{_first}\" \"{_second}\" \"{_folder}\"");
        dropped.Count.ShouldBe(3);
        dropped.Skipped.ShouldBe(0);
    }

    [Fact]
    public void Missing_relative_and_device_paths_are_skipped()
    {
        var dropped = DroppedPaths.Format(
            [_first, Path.Combine(_root.FullName, "yok.pdf"), "göreli.pdf", @"\\.\PhysicalDrive0", ""]).Value;

        dropped.Text.ShouldBe($"\"{_first}\"");
        dropped.Count.ShouldBe(1);
        dropped.Skipped.ShouldBe(4);
    }

    [Fact]
    public void Nothing_usable_is_an_error()
    {
        DroppedPaths.Format([]).Error!.Code.ShouldBe("DropNothingUsable");
        DroppedPaths.Format(["göreli.pdf"]).Error!.Code.ShouldBe("DropNothingUsable");
    }

    [Fact]
    public void Huge_drops_are_refused()
    {
        DroppedPaths.Format(Enumerable.Repeat(_first, DroppedPaths.MaxCount)).Value.Count.ShouldBe(DroppedPaths.MaxCount);
        DroppedPaths.Format(Enumerable.Repeat(_first, DroppedPaths.MaxCount + 1)).Error!.Code.ShouldBe("DropTooMany");
    }

    public void Dispose() => _root.Delete(recursive: true);
}
