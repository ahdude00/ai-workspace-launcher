using AWL.Core.Shell;

namespace AWL.Core.Tests.Shell;

public sealed class LaunchArgumentParserTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("awl-launch-");
    private readonly string _folder;
    private readonly string _file;

    public LaunchArgumentParserTests()
    {
        // Spaces, Turkish letters and a semicolon must survive every step (SAG-06).
        _folder = Directory.CreateDirectory(Path.Combine(_root.FullName, "İş Dosyaları; 2026")).FullName;
        _file = Path.Combine(_folder, "Cevap Dilekçesi (ş).udf");
        File.WriteAllText(_file, "x");
    }

    [Fact]
    public void No_arguments_only_activates()
    {
        var request = LaunchArgumentParser.Parse([]).Value;
        request.Kind.ShouldBe(LaunchKind.Activate);
        request.Path.ShouldBeNull();
        request.Folder.ShouldBeNull();
    }

    [Fact]
    public void Folder_requests_keep_the_full_path()
    {
        var request = LaunchArgumentParser.Parse(["--folder", _folder]).Value;
        request.Kind.ShouldBe(LaunchKind.Folder);
        request.Path.ShouldBe(_folder);
        request.Folder.ShouldBe(_folder);
    }

    [Fact]
    public void File_requests_use_the_file_folder()
    {
        var request = LaunchArgumentParser.Parse(["--file", _file]).Value;
        request.Kind.ShouldBe(LaunchKind.File);
        request.Path.ShouldBe(_file);
        request.Folder.ShouldBe(_folder);
    }

    [Fact]
    public void Paths_are_normalized()
    {
        string dotted = Path.Combine(_folder, "..", Path.GetFileName(_folder));
        LaunchArgumentParser.Parse(["--folder", dotted]).Value.Path.ShouldBe(_folder);
    }

    public static TheoryData<string[]> Malformed => new()
    {
        { ["--folder"] },
        { ["--folders", "x"] },
        { ["--FOLDER", "x"] },
        { ["/folder", "x"] },
        { ["--folder", "a", "--file", "b"] },
        { ["--folder", "a", "extra"] },
        { ["x", "--folder"] },
    };

    [Theory]
    [MemberData(nameof(Malformed))]
    public void Unknown_missing_or_extra_arguments_are_rejected(string[] args)
    {
        LaunchArgumentParser.Parse(args).Error!.Code.ShouldBe("LaunchArgumentsInvalid");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("göreli\\klasör")]
    [InlineData("relative/folder")]
    [InlineData(@"\\.\PhysicalDrive0")]
    [InlineData(@"\\?\C:\Windows")]
    [InlineData("//./pipe/AWL")]
    [InlineData(@"\??\C:\Windows")]
    public void Relative_and_device_paths_are_rejected(string path)
    {
        LaunchArgumentParser.Parse(["--folder", path]).Error!.Code.ShouldBe("LaunchArgumentsInvalid");
        LaunchArgumentParser.Parse(["--file", path]).Error!.Code.ShouldBe("LaunchArgumentsInvalid");
    }

    [Fact]
    public void Control_characters_and_huge_input_are_rejected()
    {
        LaunchArgumentParser.Parse(["--folder", _folder + "\0x"]).Error!.Code.ShouldBe("LaunchArgumentsInvalid");
        LaunchArgumentParser.Parse(["--folder", _folder + new string('a', LaunchRequest.MaxPathLength)]).Error!.Code
            .ShouldBe("LaunchArgumentsInvalid");
    }

    [Fact]
    public void The_path_must_exist_as_the_requested_kind()
    {
        LaunchArgumentParser.Parse(["--folder", _file]).Error!.Code.ShouldBe("LaunchPathMissing");
        LaunchArgumentParser.Parse(["--file", _folder]).Error!.Code.ShouldBe("LaunchPathMissing");
        LaunchArgumentParser.Parse(["--folder", Path.Combine(_folder, "yok")]).Error!.Code.ShouldBe("LaunchPathMissing");
    }

    [Fact]
    public void Activation_never_carries_a_path()
    {
        LaunchRequest.Create(LaunchKind.Activate, _folder).Error!.Code.ShouldBe("LaunchArgumentsInvalid");
        LaunchRequest.Create((LaunchKind)7, _folder).Error!.Code.ShouldBe("LaunchArgumentsInvalid");
    }

    public void Dispose() => _root.Delete(recursive: true);
}
