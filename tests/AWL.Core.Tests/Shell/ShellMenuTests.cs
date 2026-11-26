using AWL.Core.Shell;

namespace AWL.Core.Tests.Shell;

public sealed class ShellMenuTests
{
    // Spaces, Turkish letters and a semicolon in the install path (SAG-06).
    private static readonly string Exe = Path.Combine(Path.GetTempPath(), "Programlar; Ağ Çalışması", "AWL.exe");

    [Fact]
    public void Folder_background_and_six_file_types_are_registered()
    {
        var entries = ShellMenu.Entries(Exe);

        entries.Select(e => e.ClassKey).ShouldBe(
        [
            "Directory", @"Directory\Background",
            @"SystemFileAssociations\.pdf", @"SystemFileAssociations\.docx", @"SystemFileAssociations\.xlsx",
            @"SystemFileAssociations\.udf", @"SystemFileAssociations\.png", @"SystemFileAssociations\.jpg",
        ]);
        entries.ShouldAllBe(e => e.VerbKey == e.ClassKey + @"\shell\AWL");
    }

    [Fact]
    public void Commands_quote_the_executable_and_the_path()
    {
        var entries = ShellMenu.Entries(Exe);

        entries[0].Command.ShouldBe($"\"{Exe}\" --folder \"%1\"");
        entries[1].Command.ShouldBe($"\"{Exe}\" --folder \"%V\"");
        entries.Skip(2).ShouldAllBe(e => e.Command == $"\"{Exe}\" --file \"%1\"");
        ShellMenu.Icon(Exe).ShouldBe($"\"{Exe}\",0");
    }

    [Theory]
    [InlineData("")]
    [InlineData("AWL.exe")]
    [InlineData("relative\\AWL.exe")]
    public void The_executable_must_be_a_full_path(string path)
    {
        Should.Throw<ArgumentException>(() => ShellMenu.Entries(path));
    }

    [Fact]
    public void A_quote_in_the_executable_path_is_rejected()
    {
        Should.Throw<ArgumentException>(() => ShellMenu.Entries(Exe + "\" --folder \"x"));
    }
}
